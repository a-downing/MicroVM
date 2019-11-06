﻿using System;
using System.Diagnostics;
using MicroVM;

namespace MicroVM
{
    class Program
    {
        static void Print(string msg) {
            Console.WriteLine(msg);
        }

        static void PrintVar<T>(string name, T var) {
            Print($"{name}: {var}");
        }

        static void Main(string[] args)
        {
            Stopwatch stopWatch = new Stopwatch();
            var assembler = new MicroVM.Assembler();
            var cpu = new MicroVM.CPU(new Peripheral(), 0x80000000);

            // some tests to make sure things are working
            bool success = assembler.Compile(@"
            .word x 33
            .word y 34
            .word z 35

            func:
                ret

            main:
                mov r0 42
                cmpi r0 42
                jmp.ne 1001

                mov r0 42
                push r0
                call func
                pop r1
                cmpi r0 r1
                jmp.ne 1002

                ldr r0 x
                cmpi r0 33
                jmp.ne 1003

                mov r15 3.14
                str r15 y
                ldr r14 y
                cmpi r14 r15
                jmp.ne 1004

                mov r0 -1
                mov r1 2
                cmpi r0 r1
                jmp.ge 1005

                # test peripheral io (reads/writes to addresses >= peripheralBase)
                mov r0 0xdeadbeef
                str r0 0xbeefdead
                ldr r0 0xbeefdead
                cmpu r0 0xdeadbeef
                jmp.ne 1006
                
                # uncomment for performance test
                #jmp main
            ", 1024);

            if(!success) {
                foreach(var error in assembler.errors) {
                    Print(error);
                }

                return;
            }

            assembler.LoadProgramToCPU(cpu);
            MicroVM.CPU.Status st;

            // pc will be one of the 1000, 1001... codes to identify the bugs on failure
            if(!cpu.Cycle(out st, 1000)) {
                if(st == MicroVM.CPU.Status.OUT_OF_INSTRUCTIONS) {
                    Print($"program finished");
                } else {
                    Print($"cpu error: {st.ToString()}");
                }
                
                Print($"cpu.pc: {cpu.pc}");
            }

            // simple performace test, currently ~70 million instructions/s
            /*const int numCycles = 1000000;
            stopWatch.Start();

            for(int i = 0; i < numCycles / 100; i++) {
                if(!cpu.Cycle(out st, 1000)) {
                    if(st == MicroVM.CPU.Status.OUT_OF_INSTRUCTIONS) {
                        Print($"program finished");
                    } else {
                        Print($"cpu error: {st.ToString()}");
                    }
                    
                    Print($"cpu.pc: {cpu.pc}");
                    break;
                }
            }

            stopWatch.Stop();
            double elapsedTime = stopWatch.Elapsed.TotalMilliseconds / 1000;
            Print($"{numCycles} in {elapsedTime}s ({(float)(numCycles / elapsedTime)} instructions/s)");*/
        }

        class Peripheral : MicroVM.CPU.IPeripheral {
            CPU.Value32 data = new CPU.Value32{ Uint = 0 };

            public CPU.Value32 Read(uint addr) {
                Print($"Read(uint addr: 0x{addr.ToString("X").PadLeft(8, '0')}) -> 0x{data.Uint.ToString("X").PadLeft(8, '0')}");
                return data;
            }

            public void Write(uint addr, CPU.Value32 value) {
                Print($"Write(uint addr: 0x{addr.ToString("X").PadLeft(8, '0')}, uint value: 0x{value.Uint.ToString("X").PadLeft(8, '0')})");
                data = value;
            }
        }
    }
}
