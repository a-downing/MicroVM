using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace MicroVM {
    class Assembler {
        List<Statement> statements = new List<Statement>();
        public Dictionary<string, Symbol> symbols = new Dictionary<string, Symbol>();
        public List<string> errors = new List<string>();
        public List<byte> programData = new List<byte>();
        public List<Instruction> instructions = new List<Instruction>();
        public List<uint> code = new List<uint>();
        public byte[] memory = null;
        public int numInstructions;

        static void Print(string msg) {
            Console.WriteLine(msg);
        }

        static void PrintVar<T>(string name, T var) {
            Print($"{name}: {var}");
        }

        public void Reset() {
            statements.Clear();
            symbols.Clear();
            errors.Clear();
            programData.Clear();
            instructions.Clear();
            code.Clear();
            memory = null;
        }

        struct Statement {
            public int lineNum;
            public string[] line;
            public Token[] tokens;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Variable {
            [FieldOffset(0)]
            public CPU.Value32 val32;
            [FieldOffset(4)]
            public Type type;
            [FieldOffset(8)]
            public int size;

            public enum Type {
                NONE, UNKNOWN, INT, UINT, BYTE, FLOAT
            }
        }

        struct Token {
            public enum Type {
                NONE,
                DIRECTIVE,
                LABEL,
                INSTRUCTION,
                IDENTIFIER,
                INTEGER,
                FLOAT
            }

            public int offset;
            public Type type;
            public string stringValue;
            public Variable var;
            public string cond;
        }

        public struct Symbol {
            public string name;
            public Variable var;
            public int labelInstructionIndex;
            public Type type;

            public enum Type {
                NONE,
                LABEL,
                LITERAL,
                CONSTANT,
                REGISTER
            }
        }

        public void LoadProgramToCPU(CPU cpu) {
            cpu.Reset();
            cpu.instructions = new uint[numInstructions];
            cpu.memory = new byte[memory.Length];
            memory.CopyTo(cpu.memory, 0);
            cpu.registers[(int)CPU.Register.SP] = (uint)programData.Count;
            cpu.pc = symbols["main"].var.val32.Uint;
            int instructionIndex = 0;

            for(int i = 0; i < instructions.Count; i++) {
                var instruction = instructions[i];
                cpu.instructions[instructionIndex++] = instruction.Create();

                if(instruction.additionalInstructions == null) {
                    continue;
                }

                for(int j = 0; j < instruction.additionalInstructions.Length; j++) {
                    cpu.instructions[instructionIndex++] = instruction.additionalInstructions[j];
                }
            }

            cpu.flags |= (uint)CPU.Flag.READY;
        }

        public struct Instruction {
            public CPU.Opcode opcode;
            public CPU.Cond cond;
            public List<CPU.Register> operands;
            public Symbol immediate;
            public int address;
            public uint[] additionalInstructions;

            public int AdditionalInstructions(bool ignoreLabelAddresses) {
                if(immediate.type == Symbol.Type.NONE || (immediate.type == Symbol.Type.LABEL && ignoreLabelAddresses)) {
                    return 0;
                }

                if(immediate.var.type == Variable.Type.FLOAT) {
                    return 1;
                } else if(immediate.var.type == Variable.Type.UNKNOWN) {
                    return immediate.var.size / sizeof(uint);
                } else if(immediate.var.type == Variable.Type.INT) {
                    if(immediate.var.val32.Uint >= GetMaxImmediateValue(operands.Count + 1)) {
                        return 1;
                    }
                }

                return 0;
            }

            public uint Create() {
                uint instruction = (uint)cond << (int)CPU.Instruction.COND_SHIFT;
                instruction |= (uint)opcode << (int)CPU.Instruction.OPCODE_SHIFT;

                for(int i = 0; i < operands.Count; i++) {
                    switch(i) {
                        case 0:
                            instruction |= (uint)((uint)operands[i] << (int)CPU.Instruction.OP1_SHIFT);
                            instruction |= (uint)CPU.Instruction.OP1_FLAG_MASK;
                            break;
                        case 1:
                            instruction |= (uint)((uint)operands[i] << (int)CPU.Instruction.OP2_SHIFT);
                            instruction |= (uint)CPU.Instruction.OP2_FLAG_MASK;
                            break;
                        case 2:
                            instruction |= (uint)((uint)operands[i] << (int)CPU.Instruction.OP3_SHIFT);
                            instruction |= (uint)CPU.Instruction.OP3_FLAG_MASK;
                            break;
                    }
                }

                instruction |= immediate.var.val32.Uint;

                return instruction;
            }
        }

        public bool Compile(string code, int memorySize) {
            Reset();

            if(!Preprocess(code)) {
                return false;
            }

            if(!Tokenize()) {
                return false;
            }

            if(!Parse()) {
                return false;
            }

            if(!symbols.ContainsKey("main")) {
                errors.Add($"program must define \"main:\" entry point");
                return false;
            }

            if(!GenerateCode()) {
                return false;
            }

            if(memorySize < programData.Count) {
                errors.Add($"program data is larger than desired memory size ({memorySize} < {programData.Count})");
                return false;
            }

            memory = new byte[memorySize];
            programData.CopyTo(memory, 0);

            return true;
        }

        static bool TryStringToOpcode(string str, out CPU.Opcode opcode) {
            var names = Enum.GetNames(typeof(CPU.Opcode));
            var values = Enum.GetValues(typeof(CPU.Opcode));
            str = str.ToUpperInvariant();
            
            for(int i = 0; i < names.Length; i++) {
                if(names[i] == str) {
                    opcode = (CPU.Opcode)values.GetValue(i);
                    return true;
                }
            }

            opcode = 0;
            return false;
        }

        static bool TryStringToCond(string str, out CPU.Cond cond) {
            var names = Enum.GetNames(typeof(CPU.Cond));
            var values = Enum.GetValues(typeof(CPU.Cond));
            str = str.ToUpperInvariant();
            
            for(int i = 0; i < names.Length; i++) {
                if(names[i] == str) {
                    cond = (CPU.Cond)values.GetValue(i);
                    return true;
                }
            }

            cond = 0;
            return false;
        }

        void AllocateRegisters() {
            var names = Enum.GetNames(typeof(CPU.Register));

            for(int i = 0; i < names.Length; i++) {
                var name = names[i].ToLowerInvariant();

                symbols.Add(name, new Symbol {
                    name = name,
                    var = new Variable{ val32 = new CPU.Value32{ Int = i } },
                    type = Symbol.Type.REGISTER
                });
            }
        }

        void AddData(Variable var, int size) {
            //todo: don't ignore size
            programData.Add(var.val32.byte0);
            programData.Add(var.val32.byte1);
            programData.Add(var.val32.byte2);
            programData.Add(var.val32.byte3);
        }

        static uint GetMaxImmediateValue(int argNum) {
            switch(argNum) {
                case 1:
                    return (uint)CPU.Instruction.IMM1_MASK;
                case 2:
                    return (uint)CPU.Instruction.IMM2_MASK;
                case 3:
                    return (uint)CPU.Instruction.IMM3_MASK;
            }

            return 0;
        }

        void AddError(int line, string error) {
            errors.Add($"[error] line {line}: {error}");
        }

        bool GenerateCode() {
            numInstructions = 0;

            // first pass, can't update label addresses yet
            for(int i = 0; i < instructions.Count; i++) {
                Instruction instruction = instructions[i];
                int additionalInstructions = instruction.AdditionalInstructions(true);

                if(additionalInstructions != 0) {
                    instruction.additionalInstructions = new uint[additionalInstructions];

                    //todo: support more than int, uint, and float
                    instruction.additionalInstructions[0] = instruction.immediate.var.val32.Uint;
                    instruction.immediate.var.val32.Uint = GetMaxImmediateValue(instruction.operands.Count + 1);
                }

                instruction.address = numInstructions;
                numInstructions += 1 + additionalInstructions;

                instructions[i] = instruction;
            }

            int growth = 0;

            for(int i = 0; i < instructions.Count; i++) {
                Instruction instruction = instructions[i];
                
                if(instruction.immediate.type == Symbol.Type.LABEL) {
                    uint maxValue = GetMaxImmediateValue(instruction.operands.Count + 1);
                    var immediate = instruction.immediate;
                    Instruction target = instructions[instruction.immediate.labelInstructionIndex];

                    if(target.address + growth >= maxValue) {
                        growth++;
                        numInstructions++;
                        instruction.additionalInstructions = new uint[1];
                        instruction.additionalInstructions[0] = (uint)(target.address + growth);
                        instruction.immediate.var.val32.Uint = maxValue;
                    } else {
                        instruction.immediate.var.val32.Int = target.address + growth;
                    }

                    if(!symbols.ContainsKey(instruction.immediate.name)) {
                        errors.Add($"missing symbol \"{instruction.immediate.name}\" (this should never happen)");
                        return false;
                    }

                    var symbol = symbols[instruction.immediate.name];
                    symbol.var.val32.Int = target.address + growth;
                    symbols[instruction.immediate.name] = symbol;
                }

                instructions[i] = instruction;

                //Print($"{instruction.opcode} [{String.Join(", ", instruction.operands)}] {instruction.immediate.word.Uint} [{(instruction.additionalInstructions == null ? "" : String.Join(", ", instruction.additionalInstructions))}]");
            }

            return true;
        }

        bool Parse() {
            AllocateRegisters();
            numInstructions = 0;

            for(int i = 0; i < statements.Count; i++) {
                var statement = statements[i];

                if(statement.tokens[0].type == Token.Type.LABEL) {
                    if(symbols.ContainsKey(statement.tokens[0].stringValue)) {
                        AddError(statement.lineNum, $"redefinition of identifier \"{statement.tokens[0].stringValue}\"");
                        return false;
                    } else {
                        symbols.Add(statement.tokens[0].stringValue, new Symbol {
                            name = statement.tokens[0].stringValue,
                            var = new Variable {
                                type = Variable.Type.NONE
                            },
                            labelInstructionIndex = numInstructions,
                            type = Symbol.Type.LABEL
                        });
                    }
                } else if(statement.tokens[0].type == Token.Type.INSTRUCTION) {
                    numInstructions++;
                }
            }

            for(int i = 0; i < statements.Count; i++) {
                var statement = statements[i];

                if(statement.tokens[0].type == Token.Type.DIRECTIVE) {
                    if(statement.tokens[0].stringValue == "const" || statement.tokens[0].stringValue == "word") {
                        if(statement.tokens.Length != 3 || statement.tokens[1].type != Token.Type.IDENTIFIER || (statement.tokens[2].type != Token.Type.INTEGER && statement.tokens[2].type != Token.Type.FLOAT)) {
                            AddError(statement.lineNum, $"invalid directive");
                            return false;
                        }

                        if(symbols.ContainsKey(statement.tokens[1].stringValue)) {
                            AddError(statement.lineNum, $"redefinition of identifier \"{statement.tokens[1].stringValue}\"");
                            return false;
                        } else {
                            if(statement.tokens[0].stringValue == "const") {
                                symbols.Add(statement.tokens[1].stringValue, new Symbol {
                                    name = statement.tokens[1].stringValue,
                                    var = statement.tokens[2].var,
                                    type = Symbol.Type.CONSTANT
                                });
                            } else {
                                int addr = programData.Count;
                                AddData(statement.tokens[2].var, 4);

                                symbols.Add(statement.tokens[1].stringValue, new Symbol {
                                    name = statement.tokens[1].stringValue,
                                    var = new Variable{ val32 = new CPU.Value32{ Int = addr }},
                                    type = Symbol.Type.CONSTANT
                                });
                            }
                        }
                    } else if(statement.tokens[0].stringValue == "isr") {
                        if(statement.tokens.Length != 3 || statement.tokens[1].type != Token.Type.IDENTIFIER ||statement.tokens[2].type != Token.Type.IDENTIFIER) {
                            AddError(statement.lineNum, $"invalid directive");
                            return false;
                        }

                        Symbol labelAddr0;
                        Symbol labelAddr1;

                        if(!symbols.TryGetValue(statement.tokens[1].stringValue, out labelAddr0)) {
                            AddError(statement.lineNum, $"invalid isr directive, no identifier \"{statement.tokens[1].stringValue}\"");
                            return false;
                        } else if(labelAddr0.type != Symbol.Type.LITERAL) {
                            AddError(statement.lineNum, $"invalid isr directive, identifier \"{statement.tokens[1].stringValue}\" is not an address");
                            return false;
                        }

                        if(!symbols.TryGetValue(statement.tokens[2].stringValue, out labelAddr1)) {
                            AddError(statement.lineNum, $"invalid isr directive, no identifier \"{statement.tokens[2].stringValue}\"");
                            return false;
                        } else if(labelAddr1.type != Symbol.Type.LITERAL) {
                            AddError(statement.lineNum, $"invalid isr directive, identifier \"{statement.tokens[2].stringValue}\" is not an address");
                            return false;
                        }

                        //var inst = instructions[labelAddr0.word.Int + 1];
                        //inst = labelAddr1.word.Uint;
                        //instructions[labelAddr0.word.Int + 1] = inst;
                        AddError(statement.lineNum, $"isr is unfinished");
                    } else {
                        AddError(statement.lineNum, $"unknown directive \"{statement.tokens[0].stringValue}\"");
                        return false;
                    }
                } else if(statement.tokens[0].type == Token.Type.INSTRUCTION) {
                    var instruction = new Instruction {
                        opcode = 0,
                        cond = 0,
                        operands = new List<CPU.Register>(),
                        address = 0,
                        additionalInstructions = null,
                        immediate = new Symbol {
                            var = new Variable{ type = Variable.Type.NONE },
                            type = Symbol.Type.NONE
                        }
                    };

                    if(!TryStringToOpcode(statement.tokens[0].stringValue, out instruction.opcode)) {
                        AddError(statement.lineNum, $"unknown opcode \"{statement.tokens[0].stringValue}\"");
                        return false;
                    }

                    if(!TryStringToCond(statement.tokens[0].cond, out instruction.cond)) {
                        AddError(statement.lineNum, $"unknown condition \"{statement.tokens[0].cond}\"");
                        return false;
                    }

                    for(int j = 1; j < statement.tokens.Length; j++) {
                        if(statement.tokens[j].type == Token.Type.IDENTIFIER) {
                            Symbol symbol;

                            if(!symbols.TryGetValue(statement.tokens[j].stringValue, out symbol)) {
                                AddError(statement.lineNum, $"unknown identifier \"{statement.tokens[j].stringValue}\"");
                                return false;
                            }

                            if(symbol.type == Symbol.Type.REGISTER) {
                                instruction.operands.Add((CPU.Register)symbol.var.val32.Uint);
                            } else {
                                instruction.immediate = symbol;
                            }
                        } else if(statement.tokens[j].type == Token.Type.INTEGER || statement.tokens[j].type == Token.Type.FLOAT) {
                            instruction.immediate = new Symbol {
                                var = statement.tokens[j].var,
                                type = Symbol.Type.LITERAL
                            };
                        } else {
                            AddError(statement.lineNum, $"invalid instruction argument \"{statement.tokens[j].stringValue}\"");
                            return false;
                        }
                    }

                    instructions.Add(instruction);
                }
            }

            return true;
        }

        static bool TryParseIntegerLiteral(string str, out int value) {
            Match decimalMatch = Regex.Match(str, @"^[+-]?[0-9]+$");
            Match hexMatch = Regex.Match(str, @"^([+-])?0x([0-9a-zA-Z]+)$");
            Match binMatch = Regex.Match(str, @"^([+-])?0b([01]+)$");

            if(decimalMatch.Success) {
                value = Convert.ToInt32(decimalMatch.Groups[0].ToString(), 10);
                return true;
            } else if(hexMatch.Success) {
                str = hexMatch.Groups[0].ToString();
                value = Convert.ToInt32(hexMatch.Groups[2].ToString(), 16);

                if(hexMatch.Groups[1].ToString() == "-") {
                    value *= -1;
                }

                return true;
            } else if(binMatch.Success) {
                str = binMatch.Groups[0].ToString();
                value = Convert.ToInt32(binMatch.Groups[2].ToString(), 2);

                if(binMatch.Groups[1].ToString() == "-") {
                    value *= -1;
                }

                return true;
            }

            value = 0;
            return false;
        }

        bool Tokenize() {       
            for(int i = 0; i < statements.Count; i++) {
                var statement = statements[i];

                for(int j = 0; j < statement.line.Length; j++) {
                    string arg = statement.line[j];

                    statement.tokens[j].offset = 0;
                    statement.tokens[j].type = Token.Type.NONE;

                    Match directiveMatch = Regex.Match(arg, @"^\.([a-zA-Z_][a-zA-Z0-9_]*)$");
                    Match labelMatch = Regex.Match(arg, @"^([a-zA-Z_][a-zA-Z0-9_]*):$");
                    Match identifierMatch = Regex.Match(arg, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
                    Match instructionMatch = Regex.Match(arg, @"^([a-zA-Z_][a-zA-Z0-9_]*)\.(al|eq|ne|gt|ge|lt|le)$");
                    Match floatMatch = Regex.Match(arg, @"^[+-]?[0-9]?[\.][0-9]*$");

                    if(directiveMatch.Success) {
                        statement.tokens[j].type = Token.Type.DIRECTIVE;
                        statement.tokens[j].stringValue = directiveMatch.Groups[1].ToString();
                    } else if(labelMatch.Success) {
                        statement.tokens[j].type = Token.Type.LABEL;
                        statement.tokens[j].stringValue = labelMatch.Groups[1].ToString();
                    } else if(identifierMatch.Success) {
                        statement.tokens[j].type = (j == 0) ? Token.Type.INSTRUCTION : Token.Type.IDENTIFIER;
                        statement.tokens[j].stringValue = identifierMatch.Groups[0].ToString();

                        if(statement.tokens[j].type == Token.Type.INSTRUCTION) {
                            statement.tokens[j].cond = "al";
                        }
                    } else if(instructionMatch.Success) {
                        statement.tokens[j].type = Token.Type.INSTRUCTION;
                        statement.tokens[j].stringValue = instructionMatch.Groups[1].ToString();
                        statement.tokens[j].cond = instructionMatch.Groups[2].ToString();
                    } else if(floatMatch.Success && floatMatch.Groups[0].ToString() != ".") {
                        statement.tokens[j].type = Token.Type.FLOAT;
                        statement.tokens[j].stringValue = floatMatch.Groups[0].ToString();
                        float.TryParse(statement.tokens[j].stringValue, out statement.tokens[j].var.val32.Float);
                        statement.tokens[j].var.type = Variable.Type.FLOAT;
                    } else if(TryParseIntegerLiteral(arg, out statement.tokens[j].var.val32.Int)) {
                        statement.tokens[j].type = Token.Type.INTEGER;
                        statement.tokens[j].stringValue = arg;
                        statement.tokens[j].var.type = Variable.Type.INT;
                    }
                }
            }

            return true;
        }

        bool Preprocess(string code) {
            var lines = code.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);
            
            for(int i = 0; i < lines.Length; i++) {
                lines[i] = code = Regex.Replace(lines[i].Trim() ,@"\s+"," ");

                if(lines[i].Length > 0) {
                    if(lines[i][0] == '#') {
                        continue;
                    }

                    var args = lines[i].Split(' ');

                    if(args.Length > 0) {
                        statements.Add(new Statement {
                            lineNum = i,
                            line = args,
                            tokens = new Token[args.Length]
                        });
                    }
                }
            }

            return true;
        }
    }
}