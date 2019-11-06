using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MicroVM {
    class CPU {
        public uint[] registers = new uint[64];
        public byte[] memory = null;
        public uint[] instructions = null;
        public uint flags = (uint)Flag.INTERRUPTS_ENABLED;
        Queue<uint> pendingInterrupts = new Queue<uint>();
        public IPeripheral peripheral = null;
        static int maxPendingInterrupts = 8;
        System.Random random = new System.Random();
        public uint pc = 0;
        public uint peripheralBase = 0;

        static void Print(string msg) {
            Console.WriteLine(msg);
        }

        static void PrintVar<T>(string name, T var) {
            Print($"{name}: {var}");
        }

        public CPU(IPeripheral peripheral, uint peripheralBase) {
            this.peripheral = peripheral;
            this.peripheralBase = peripheralBase;
        }

        public void Reset() {
            registers = new uint[64];
            memory = null;
            instructions = null;
            flags = (uint)Flag.INTERRUPTS_ENABLED;
            pendingInterrupts.Clear();
        }

        public interface IPeripheral {
            Value32 Read(uint addr);
            void Write(uint addr, Value32 value);
        }

        [Flags]
        public enum Flag : uint {
            INTERRUPTS_ENABLED = 1 << 0,
            EQUAL = 1 << 1,
            GREATER_THAN = 1 << 2,
            LESS_THAN = 1 << 3,
            READY = 1 << 4
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Value32 {
            [FieldOffset(0)]
            public int Int;
            [FieldOffset(0)]
            public uint Uint;
            [FieldOffset(0)]
            public float Float;
            [FieldOffset(0)]
            public byte byte0;
            [FieldOffset(1)]
            public byte byte1;
            [FieldOffset(2)]
            public byte byte2;
            [FieldOffset(3)]
            public byte byte3;
        }

        public enum Instruction {
            COND_SHIFT = 29,
            COND_MASK = 7 << (int)COND_SHIFT,
            OPCODE_SHIFT = 23,
            OPCODE_MASK = 0x3F << (int)OPCODE_SHIFT,
            OP1_FLAG_SHIFT = 22,
            OP1_FLAG_MASK = 1 << (int)OP1_FLAG_SHIFT,
            OP1_SHIFT = 16,
            OP1_MASK = 0x3F << (int)OP1_SHIFT,
            OP2_FLAG_SHIFT = 15,
            OP2_FLAG_MASK = 1 << (int)OP2_FLAG_SHIFT,
            OP2_SHIFT = 9,
            OP2_MASK = 0x3F << (int)OP2_SHIFT,
            OP3_FLAG_SHIFT = 8,
            OP3_FLAG_MASK = 1 << (int)OP3_FLAG_SHIFT,
            OP3_SHIFT = 2,
            OP3_MASK = 0x3F << (int)OP3_SHIFT,
            IMM1_SHIFT = 0,
            IMM1_MASK = 0x3FFFFF << (int)IMM1_SHIFT,
            IMM2_SHIFT = 0,
            IMM2_MASK = 0x7FFF << (int)IMM2_SHIFT,
            IMM3_SHIFT = 0,
            IMM3_MASK = 0xFF << (int)IMM3_SHIFT,
            IMM4_SHIFT = 0,
            IMM4_MASK = 0x3 << (int)IMM4_SHIFT
        }
        
        public enum Cond {
            AL, EQ, NE, GT, GE, LT, LE, RESERVED
        }

        public enum Register {
            R0, R1, R2,  R3,  R4,  R5,  R6,  R7,
            R8, R9, R10, R11, R12, R13, R14, R15,
            SP, BP
        }

        public enum Opcode {
            RET, CLI, SEI,
            JMP, JNE, CALL, PUSH, POP,
            MOV, LDR, LDRB, STR, STRB, CMPI, CMPU,
            SHRS, SHRU, SHL, AND, OR, XOR, NOT, ADD, SUB, MUL, DIV, MOD,
            RESERVED = 0x3F
        }

        public enum Status {
            SUCCESS,
            OUT_OF_INSTRUCTIONS,
            MISSING_INSTRUCTION,
            BAD_INSTRUCTION,
            SEGFAULT,
            DIVISION_BY_ZERO
        }

        public bool Interrupt(uint addr) {
            if((flags & (uint)Flag.READY) == 0) {
                return false;
            }

            if(pendingInterrupts.Count >= maxPendingInterrupts) {
                return false;
            }

            pendingInterrupts.Enqueue(addr);
            return true;
        }

        void AssignMemory(uint addr, Value32 val) {
            //Print($"AssignMemory(uint addr: {addr}, uint value: {value})");

            if(peripheral != null && addr >= peripheralBase) {
                peripheral.Write(addr, val);
                return;
            }

            memory[addr + 0] = val.byte0;
            memory[addr + 1] = val.byte1;
            memory[addr + 2] = val.byte2;
            memory[addr + 3] = val.byte3;
        }

        void AssignMemory(uint addr, uint val) {
            AssignMemory(addr, new Value32 { Uint = val });
        }

        Value32 ReadMemory(uint addr) {
            if(peripheral != null && addr >= peripheralBase) {
                return peripheral.Read(addr);
            }

            Value32 val = new Value32 {
                byte0 = memory[addr + 0],
                byte1 = memory[addr + 1],
                byte2 = memory[addr + 2],
                byte3 = memory[addr + 3]
            };

            //Print($"ReadMemory(uint addr: {addr}) -> uint: {val.Uint}");
            return val;
        }

        public bool Cycle(out Status status, int numCycles = 1) {
            for(int i = 0; i < numCycles; i++) {
                if(pc >= instructions.Length) {
                    status = Status.OUT_OF_INSTRUCTIONS;
                    return false;
                }

                if(registers[(int)Register.SP] >= memory.Length) {
                    status = Status.SEGFAULT;
                    return false;
                }

                if((flags & (uint)Flag.INTERRUPTS_ENABLED) != 0 && pendingInterrupts.Count != 0) {
                    uint addr = pendingInterrupts.Dequeue();

                    Print($"dequeued interrupt, addr: {addr}");

                    AssignMemory(registers[(int)Register.SP], pc);
                    registers[(int)Register.SP] += 4;
                    pc = addr;

                    if(pc >= instructions.Length) {

                        status = Status.OUT_OF_INSTRUCTIONS;
                        return false;
                    }

                    if(registers[(int)Register.SP] >= memory.Length) {

                        status = Status.SEGFAULT;
                        return false;
                    }
                }

                uint inst = instructions[pc++];
                Opcode opcode = (Opcode)((inst & (uint)Instruction.OPCODE_MASK) >> (int)Instruction.OPCODE_SHIFT);
                Cond cond = (Cond)((inst & (int)Instruction.COND_MASK) >> (int)Instruction.COND_SHIFT);

                /*PrintVar(nameof(pc), pc);
                Print($"instruction: {opcode}.{cond}");
                Print($"instruction bits: {Convert.ToString(inst, 2).PadLeft(32, '0')}");
                Print($"flags: {Convert.ToString(flags & (uint)Flag.EQUAL, 2).PadLeft(32, '0')}");*/

                switch(cond) {
                    case Cond.EQ:
                        if((flags & (uint)Flag.EQUAL) != 0) {
                            break;
                        }

                        continue;
                    case Cond.NE:
                        if((flags & (uint)Flag.EQUAL) == 0) {
                            break;
                        }

                        continue;
                    case Cond.GT:
                        if((flags & (uint)Flag.GREATER_THAN) != 0) {
                            break;
                        }

                        continue;
                    case Cond.LT:
                        if((flags & (uint)Flag.LESS_THAN) != 0) {
                            break;
                        }

                        continue;
                    case Cond.GE:
                        if((flags & (uint)(Flag.GREATER_THAN | Flag.EQUAL)) != 0) {
                            break;
                        }

                        continue;
                    case Cond.LE:
                        if((flags & (uint)(Flag.LESS_THAN | Flag.EQUAL)) != 0) {
                            break;
                        }

                        continue;
                }

                bool handledHere = true;

                // zero arg instructions
                switch(opcode) {
                    case Opcode.RET:
                        registers[(int)Register.SP] -= 4;
                        pc = ReadMemory(registers[(int)Register.SP]).Uint;
                        break;
                    case Opcode.CLI:
                        flags &= ~(uint)Flag.INTERRUPTS_ENABLED;
                        break;
                    case Opcode.SEI:
                        flags |= (uint)Flag.INTERRUPTS_ENABLED;
                        break;
                    default:
                        handledHere = false;
                        break;
                }

                if(handledHere) {
                    status = Status.SUCCESS;
                    continue;
                }

                uint op1 = (inst & (uint)Instruction.OP1_MASK) >> (int)Instruction.OP1_SHIFT;
                uint op1Flag = inst & (uint)Instruction.OP1_FLAG_MASK;
                uint imm1 = (inst & (uint)Instruction.IMM1_MASK) >> (int)Instruction.IMM1_SHIFT;

                if(op1Flag != 0 && imm1 == (uint)Instruction.IMM1_MASK) {
                    imm1 = instructions[pc++];
                }

                uint arg1 = (op1Flag != 0) ? registers[op1] : imm1;
                handledHere = true;

                /*PrintVar(nameof(op1), op1);
                PrintVar(nameof(op1Flag), op1Flag);
                PrintVar(nameof(imm1), imm1);
                PrintVar(nameof(arg1), arg1);*/

                // one arg instructions
                switch(opcode) {
                    case Opcode.JMP:
                        pc = arg1;
                        break;
                    case Opcode.CALL:
                        AssignMemory(registers[(int)Register.SP], pc);
                        registers[(int)Register.SP] += 4;
                        pc = arg1;
                        break;
                    case Opcode.PUSH:
                        AssignMemory(registers[(int)Register.SP], arg1);
                        registers[(int)Register.SP] += 4;
                        break;
                    case Opcode.POP:
                        registers[(int)Register.SP] -= 4;
                        registers[op1] = ReadMemory(registers[(int)Register.SP]).Uint;
                        break;
                    default:
                        handledHere = false;
                        break;
                }

                if(handledHere) {
                    status = Status.SUCCESS;
                    continue;
                }

                uint op2 = (inst & (uint)Instruction.OP2_MASK) >> (int)Instruction.OP2_SHIFT;
                uint op2Flag = inst & (uint)Instruction.OP2_FLAG_MASK;
                uint imm2 = (inst & (uint)Instruction.IMM2_MASK) >> (int)Instruction.IMM2_SHIFT;

                if(op2Flag == 0 && imm2 == (uint)Instruction.IMM2_MASK) {
                    imm2 = instructions[pc++];
                }

                uint arg2 = (op2Flag != 0) ? registers[op2] : imm2;
                handledHere = true;

                /*PrintVar(nameof(op2), op2);
                PrintVar(nameof(op2Flag), op2Flag);
                PrintVar(nameof(imm2), imm2);
                PrintVar(nameof(arg2), arg2);*/

                // two arg instructions
                switch(opcode) {
                    case Opcode.MOV:
                        registers[op1] = arg2;
                        break;
                    case Opcode.LDR:
                        registers[op1] = ReadMemory(arg2).Uint;
                        break;
                    case Opcode.STR:
                        AssignMemory(arg2, arg1);
                        break;
                    case Opcode.CMPI:
                        flags = ((int)arg1 == (int)arg2) ? flags | (uint)Flag.EQUAL : flags & ~(uint)Flag.EQUAL;
                        flags = ((int)arg1 > (int)arg2) ? flags | (uint)Flag.GREATER_THAN : flags & ~(uint)Flag.GREATER_THAN;
                        flags = ((int)arg1 < (int)arg2) ? flags | (uint)Flag.LESS_THAN : flags & ~(uint)Flag.LESS_THAN;
                        break;
                    case Opcode.CMPU:
                        flags = (arg1 == arg2) ? flags | (uint)Flag.EQUAL : flags & ~(uint)Flag.EQUAL;
                        flags = (arg1 > arg2) ? flags | (uint)Flag.GREATER_THAN : flags & ~(uint)Flag.GREATER_THAN;
                        flags = (arg1 < arg2) ? flags | (uint)Flag.LESS_THAN : flags & ~(uint)Flag.LESS_THAN;
                        break;
                    default:
                        handledHere = false;
                        break;
                }

                if(handledHere) {
                    status = Status.SUCCESS;
                    continue;
                }

                uint op3 = (inst & (uint)Instruction.OP3_MASK) >> (int)Instruction.OP3_SHIFT; 
                uint op3Flag = inst & (uint)Instruction.OP3_FLAG_MASK;
                uint imm3 = (inst & (uint)Instruction.IMM3_MASK) >> (int)Instruction.IMM3_SHIFT;

                if(op3Flag != 0 && imm3 == (uint)Instruction.IMM3_MASK) {
                    imm3 = instructions[pc++];
                }

                uint arg3 = (op3Flag != 0) ? registers[op3] : imm3;
                handledHere = true;

                /*PrintVar(nameof(op3), op3);
                PrintVar(nameof(op3Flag), op3Flag);
                PrintVar(nameof(imm3), imm3);
                PrintVar(nameof(arg3), arg3);*/

                // three arg instructions
                switch(opcode) {
                    case Opcode.SHRS:
                        registers[op1] = (uint)((int)arg2 >> (int)arg3);
                        break;
                    case Opcode.SHRU:
                        registers[op1] = arg2 >> (int)arg3;
                        break;
                    case Opcode.SHL:
                        registers[op1] = arg2 << (int)arg3;
                        break;
                    case Opcode.AND:
                        registers[op1] = arg2 & arg3;
                        break;
                    case Opcode.OR:
                        registers[op1] = arg2 | arg3;
                        break;
                    case Opcode.XOR:
                        registers[op1] = arg2 ^ arg3;
                        break;
                    case Opcode.NOT:
                        registers[op1] = ~arg2;
                        break;
                    case Opcode.ADD:
                        registers[op1] = arg2 + arg3;
                        break;
                    case Opcode.SUB:
                        registers[op1] = arg2 - arg3;
                        break;
                    case Opcode.MUL:
                        registers[op1] = arg2 * arg3;
                        break;
                    case Opcode.DIV:
                        if(arg2 == 0) {
                            status = Status.DIVISION_BY_ZERO;
                            return false;
                        }

                        registers[op1] = arg2 / arg3;
                        break;
                    case Opcode.MOD:
                        if(arg2 == 0) {
                            status = Status.DIVISION_BY_ZERO;
                            return false;
                        }

                        registers[op1] = arg2 / arg3;
                        break;
                    default:
                        handledHere = false;
                        break;
                }

                if(handledHere) {
                    status = Status.SUCCESS;
                    continue;
                }

                status = Status.MISSING_INSTRUCTION;
                return false;
            }

            status = Status.SUCCESS;
            return true;
        }
    }
}