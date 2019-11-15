using System;
using System.Diagnostics;
using MicroVM;

namespace MicroVM
{
    class Program
    {
        public static void Print(string msg) {
            Console.WriteLine(msg);
        }

        public static void PrintVar<T>(string name, T var) {
            Print($"{name}: {var}");
        }

        static void Main(string[] args)
        {
            Stopwatch stopWatch = new Stopwatch();
            var assembler = new MicroVM.Assembler();
            var cpu = new MicroVM.CPU(new Peripheral(), 0x80000000);

            // entry point, and some functions for compiled code to call into
            string asmCode = @"
            print:
                ldr r0 sp -8
                str r0 0x80000000
                ret

            randInt:
                rngi r0
                ret

            _start:
                mov bp sp
                call main

                #test strb/ldrb
                mov r0 0xaaaaaabb
                strb r0 0xdeadbeef
                ldrb r0 0xdeadbeef

                jmp end
                #jmp _start
            ";

            // code generated from compiler
            asmCode += System.IO.File.ReadAllText("code.asm");
            asmCode += "\nend:\nnop";

            bool success = assembler.Compile(asmCode, 1024);

            if(!success) {
                foreach(var error in assembler.errors) {
                    Print(error);
                }

                return;
            }

            assembler.LoadProgramToCPU(cpu);
            MicroVM.CPU.Status st;

            // test interrupt 0 (isr_0_name)
            //cpu.Interrupt(0);

            // pc will be one of the 1000, 1001... codes to identify the bugs on failure
            if(!cpu.Cycle(out st, 2000)) {
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
                if(!cpu.Cycle(out st, 100)) {
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
