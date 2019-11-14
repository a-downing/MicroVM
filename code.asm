.word x 0
.word y 0
.word z 0
main:
push bp
mov bp sp
add sp sp 16
mov r0 49
mov r2 x
ldr r1 r2
str r0 r2
mov r0 1
mov r2 y
ldr r1 r2
str r0 r2
mov r0 42
mov r2 z
ldr r1 r2
str r0 r2
mov r1 x
ldr r0 r1
mov r3 y
ldr r2 r3
add r0 r0 r2
mov r1 50
cmpi r0 r1
mov r0 0
mov.eq r0 1
cmpi r0 0
jmp.eq __and_false_1
mov r1 50
mov r3 y
ldr r2 r3
mov r5 x
ldr r4 r5
add r2 r2 r4
cmpi r1 r2
mov r1 0
mov.eq r1 1
cmpi r1 0
jmp.eq __and_false_1
mov r0 1
jmp __and_end_1
__and_false_1:
mov r0 0
__and_end_1:
cmpi r0 0
jmp.eq __if_else_0
mov r1 x
ldr r0 r1
mov r3 y
ldr r2 r3
add r0 r0 r2
str r0 bp +0
mov r1 bp
add r1 r1 0
ldr r0 r1
mov r2 1
sub r0 r0 r2
mov r2 bp
add r2 r2 0
ldr r1 r2
str r0 r2
mov r0 0x50
push r0
call foo
sub sp sp 4
mov r1 bp
add r1 r1 0
ldr r0 r1
mov r2 49
cmpi r0 r2
mov r0 0
mov.eq r0 1
cmpi r0 0
jmp.eq __if_else_2
mov r0 0x49
push r0
call foo
sub sp sp 4
jmp __if_end_2
__if_else_2:
mov r0 0xdead0049
push r0
call foo
sub sp sp 4
__if_end_2:
jmp __if_end_0
__if_else_0:
mov r0 0xdead0050
push r0
call foo
sub sp sp 4
__if_end_0:
mov r0 0
str r0 bp +4
__while_start_3:
mov r1 bp
add r1 r1 4
ldr r0 r1
mov r2 10
cmpi r0 r2
mov r0 0
mov.lt r0 1
cmpi r0 0
jmp.eq __while_end_3
mov r1 bp
add r1 r1 4
ldr r0 r1
push r0
call print
sub sp sp 4
call randInt
sub sp sp 0
push r0
call print
sub sp sp 4
mov r1 bp
add r1 r1 4
ldr r0 r1
mov r2 1
add r0 r0 r2
mov r2 bp
add r2 r2 4
ldr r1 r2
str r0 r2
jmp __while_start_3
__while_end_3:
mov r0 0
str r0 bp +8
__while_start_4:
mov r1 bp
add r1 r1 8
ldr r0 r1
mov r2 5
cmpi r0 r2
mov r0 0
mov.lt r0 1
cmpi r0 0
jmp.eq __while_end_4
mov r1 bp
add r1 r1 8
ldr r0 r1
push r0
call print
sub sp sp 4
ldr r0 bp +8
mov r1 1
add r1 r0 r1
str r1 bp +8
jmp __while_start_4
__while_end_4:
mov r0 0x333
str r0 bp +12
mov r1 bp
add r1 r1 12
ldr r0 r1
push r0
call print
sub sp sp 4
mov r1 bp
add r1 r1 12
ldr r0 r1
mov r0 r1
mov r3 bp
add r3 r3 8
ldr r2 r3
str r0 r3
mov r1 bp
add r1 r1 8
ldr r0 r1
push r0
call print
sub sp sp 4
mov r0 0x777
mov r2 bp
add r2 r2 8
ldr r1 r2
mov r2 r1
ldr r1 r1
str r0 r2
mov r1 bp
add r1 r1 12
ldr r0 r1
push r0
call print
sub sp sp 4
mov r0 0
mov sp bp
pop bp
ret
foo:
push bp
mov bp sp
add sp sp 0
mov r1 bp
sub r1 r1 12
ldr r0 r1
push r0
call print
sub sp sp 4
mov r0 0
mov sp bp
pop bp
ret
