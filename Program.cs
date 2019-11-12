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

            // some tests to make sure things are working
            bool success = assembler.Compile(@"
print:
    push bp
    mov bp sp
    ldr r0 bp -12
    str r0 0x80000000
    mov sp bp
    pop bp
    ret

_start:
mov bp sp
call main
jmp end

.word x 0
.word y 0
.word z 0
main:
push bp
mov bp sp
add sp sp 12
mov r0 49
str r0 x
mov r0 1
str r0 y
mov r0 42
str r0 z
ldr r0 x
ldr r1 y
add r0 r0 r1
mov r1 50
cmpi r0 r1
mov r0 0
mov.eq r0 1
cmpi r0 0
jmp.eq __if_else_0
ldr r0 x
ldr r1 y
add r0 r0 r1
str r0 bp +0
ldr r0 bp +0
mov r1 1
sub r0 r0 r1
str r0 bp +0
mov r0 0x50
push r0
call foo
ldr r0 bp +0
mov r1 49
cmpi r0 r1
mov r0 0
mov.eq r0 1
cmpi r0 0
jmp.eq __if_else_1
mov r0 0x49
push r0
call foo
jmp __if_end_1
__if_else_1:
mov r0 0xdead0049
push r0
call foo
__if_end_1:
jmp __if_end_0
__if_else_0:
mov r0 0xdead0050
push r0
call foo
__if_end_0:
mov r0 0
str r0 bp +4
__while_start_2:
ldr r0 bp +4
mov r1 10
cmpi r0 r1
mov r0 0
mov.lt r0 1
cmpi r0 0
jmp.eq __while_end_2
ldr r0 bp +4
push r0
call print
ldr r0 bp +4
mov r1 1
add r0 r0 r1
str r0 bp +4
jmp __while_start_2
__while_end_2:
mov r0 0
str r0 bp +8
__while_start_3:
ldr r0 bp +8
mov r1 3
cmpi r0 r1
mov r0 0
mov.lt r0 1
cmpi r0 0
jmp.eq __while_end_3
ldr r0 bp +8
push r0
call print
ldr r0 bp +8
mov r1 1
add r0 r0 r1
str r0 bp +8
jmp __while_start_3
__while_end_3:
mov r0 0
mov sp bp
pop bp
ret
foo:
push bp
mov bp sp
add sp sp 0
ldr r0 bp -12
push r0
call print
mov r0 0
mov sp bp
pop bp
ret

end:
jmp _start
nop
            ", 1024);

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
