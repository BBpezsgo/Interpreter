using System.Collections.Generic;

namespace IngameCoding.BCCode
{
    using Core;
    using Errors;

    public enum TokenSubtype
    {
        None,
        CommandName,
        Parameter,
        LabelName,
        Number,
    }

    public class Statement
    {
        public Token name;
        public List<int> intParameters = new();
        public List<string> strParameters = new();
        public List<string> identifyParameters = new();

        public int line;

        public int startOffset;
        public int endOffset;

        public int startOffsetTotal;
        public int endOffsetTotal;
    }

    public class TagDefinition
    {
        internal Token Name;
        internal int Pointer = -1;
    }

    public struct ParserResult
    {
        internal Statement[] statements;
        internal Dictionary<string, TagDefinition> tags;
    }

    public class Parser
    {
        int CurrentTokenIndex;
        Token[] Tokens;

        Token CurrentToken => (CurrentTokenIndex < Tokens.Length) ? Tokens[CurrentTokenIndex] : null;

        readonly Dictionary<string, TagDefinition> Labels = new();
        readonly List<Statement> Statements = new();

        static Bytecode.Opcode TryGetTagOverride(string name) => name switch
        {
            _ => Bytecode.Opcode.UNKNOWN,
        };

        static int GetParameterCount(Bytecode.Opcode opcode) => opcode switch
        {
            Bytecode.Opcode.EXIT => 0,
            Bytecode.Opcode.PUSH_VALUE => 1,
            Bytecode.Opcode.POP_VALUE => 0,
            Bytecode.Opcode.JUMP_BY_IF_FALSE => 1,
            Bytecode.Opcode.JUMP_BY_IF_TRUE => 1,
            Bytecode.Opcode.JUMP_BY => 1,
            Bytecode.Opcode.LOAD_VALUE => 1,
            Bytecode.Opcode.STORE_VALUE => 1,
            Bytecode.Opcode.LOAD_VALUE_BR => 1,
            Bytecode.Opcode.STORE_VALUE_BR => 1,
            Bytecode.Opcode.CALL => 1,
            Bytecode.Opcode.RETURN => 0,
            Bytecode.Opcode.CALL_BUILTIN => 0,
            Bytecode.Opcode.LOGIC_LT => 0,
            Bytecode.Opcode.LOGIC_MT => 0,
            Bytecode.Opcode.LOGIC_LTEQ => 0,
            Bytecode.Opcode.LOGIC_MTEQ => 0,
            Bytecode.Opcode.LOGIC_AND => 0,
            Bytecode.Opcode.LOGIC_OR => 0,
            Bytecode.Opcode.LOGIC_XOR => 0,
            Bytecode.Opcode.LOGIC_EQ => 0,
            Bytecode.Opcode.LOGIC_NEQ => 0,
            Bytecode.Opcode.LOGIC_NOT => 0,
            Bytecode.Opcode.MATH_ADD => 0,
            Bytecode.Opcode.MATH_SUB => 0,
            Bytecode.Opcode.MATH_MULT => 0,
            Bytecode.Opcode.MATH_DIV => 0,
            Bytecode.Opcode.MATH_MOD => 0,
            Bytecode.Opcode.LOAD_FIELD => 1,
            Bytecode.Opcode.STORE_FIELD => 1,
            Bytecode.Opcode.LOAD_FIELD_BR => 1,
            Bytecode.Opcode.STORE_FIELD_BR => 1,
            Bytecode.Opcode.LIST_INDEX => 1,
            Bytecode.Opcode.LIST_PUSH_ITEM => 0,
            Bytecode.Opcode.LIST_ADD_ITEM => 1,
            Bytecode.Opcode.LIST_PULL_ITEM => 0,
            Bytecode.Opcode.LIST_REMOVE_ITEM => 1,
            Bytecode.Opcode.TYPE_GET => 0,
            Bytecode.Opcode.COMMENT => -1,
            Bytecode.Opcode.UNKNOWN => -1,
            _ => -1,
        };

        static Bytecode.Opcode GetOpcode(string text, Token token)
        {
            if (TryGetTagOverride(text.ToLower()) != Bytecode.Opcode.UNKNOWN)
            { return TryGetTagOverride(text.ToLower()); }

            foreach (Bytecode.Opcode color in System.Enum.GetValues(typeof(Bytecode.Opcode)))
            {
                if (color.ToString() == text.ToUpper())
                { return color; }
            }

            throw new CompilerException("There is no opcode with name '" + text + "'", token);
        }
        static Bytecode.Opcode GetOpcode(string text, int line)
        {
            if (TryGetTagOverride(text.ToLower()) != Bytecode.Opcode.UNKNOWN)
            { return TryGetTagOverride(text.ToLower()); }

            foreach (Bytecode.Opcode color in System.Enum.GetValues(typeof(Bytecode.Opcode)))
            {
                if (color.ToString() == text.ToUpper())
                { return color; }
            }

            throw new CompilerException("There is no opcode with name '" + text + "'", new Position(line));
        }

        public static Bytecode.Instruction[] GenerateCode(ParserResult parserResult)
        {
            List<Bytecode.Instruction> instructions = new();
            for (int line = 0; line < parserResult.statements.Length; line++)
            {
                Statement statement = parserResult.statements[line];
                Bytecode.Instruction instruction = new(GetOpcode(statement.name.text, statement.line), statement.line);

                int paramCount = statement.intParameters.Count + statement.strParameters.Count + statement.identifyParameters.Count;
                if (paramCount != GetParameterCount(instruction.opcode))
                {
                    throw new CompilerException("Opcode '" + statement.name + "' needs " + GetParameterCount(instruction.opcode).ToString() + " parameters", new Position(statement.line, statement.startOffset));
                }

                if (instruction.opcode == Bytecode.Opcode.JUMP_BY_IF_FALSE || instruction.opcode == Bytecode.Opcode.JUMP_BY || instruction.opcode == Bytecode.Opcode.CALL)
                {
                    if (statement.intParameters.Count == 0)
                    {
                        if (statement.identifyParameters.Count == 1)
                        {
                            if (parserResult.tags.TryGetValue(statement.identifyParameters[0], out TagDefinition tag))
                            {
                                instruction.parameter = tag.Pointer - line;
                            }
                            else
                            {
                                throw new System.Exception("Label " + statement.identifyParameters[0] + " not found");
                            }
                        }
                    }
                    else
                    {
                        instruction.parameter = statement.intParameters[0];
                    }
                }
                else if (statement.intParameters.Count > 0)
                {
                    instruction.parameter = statement.intParameters[0];
                }

                instructions.Add(instruction);
            }
            return instructions.ToArray();
        }

        public ParserResult Parse(List<Token> tokens) => Parse(tokens.ToArray());
        public ParserResult Parse(Token[] tokens)
        {
            this.Tokens = tokens;
            this.CurrentTokenIndex = 0;

            int endlessSafe = 0;
            while (CurrentToken != null)
            {
                ConsumeLinebreaks();

                if (CurrentToken == null) break;

                if (ExpectLabel())
                { }
                else
                {
                    Statement statement = ExpectCommand();
                    if (statement != null) Statements.Add(statement);
                }

                endlessSafe++;
                if (endlessSafe > 500) { throw new EndlessLoopException(); }
            }

            return new ParserResult()
            {
                statements = this.Statements.ToArray(),
                tags = this.Labels,
            };
        }

        bool ExpectLabel()
        {
            int parseStart = CurrentTokenIndex;

            Token possibleName = ExpectIdentifier();

            if (possibleName == null)
            {
                CurrentTokenIndex = parseStart;
                return false;
            }

            if (ExpectOperator(":", out Token endOperator) == null)
            {
                CurrentTokenIndex = parseStart;
                return false;
            }

            possibleName.subtype = TokenSubtype.LabelName;

            Labels.Add(possibleName.text, new TagDefinition()
            {
                Name = possibleName,
                Pointer = Statements.Count,
            });

            if (ExpectLinebreak() == null)
            { throw new SyntaxException("there must be a linebreak after a label", possibleName); }

            return true;
        }

        Token ExpectInt(out bool isNegative)
        {
            isNegative = false;

            if (CurrentToken == null) return null;

            if (CurrentToken.type == TokenType.LITERAL_INTEGER)
            {
                Token integerToken = CurrentToken.Clone();
                CurrentTokenIndex++;
                return integerToken;
            }
            else if (ExpectOperator("-", out Token minusToken) != null)
            {
                minusToken.subtype = TokenSubtype.Number;
                Token num = ExpectInt(out bool _);
                if (num != null)
                {
                    isNegative = true;
                    return num;
                }
            }

            return null;
        }
        Token ExpectString()
        {
            if (CurrentToken == null) return null;
            if (CurrentToken.type != TokenType.LITERAL_STRING) return null;

            Token integerToken = CurrentToken.Clone();
            CurrentTokenIndex++;
            return integerToken;
        }

        Statement ExpectCommand()
        {
            int startTokenIndex = CurrentTokenIndex;

            Token possibleFunctionName = ExpectIdentifier();
            if (possibleFunctionName == null)
            {
                CurrentTokenIndex = startTokenIndex;
                return null;
            }

            var OpCode = GetOpcode(possibleFunctionName.text, possibleFunctionName);

            var paramCount = GetParameterCount(OpCode);

            Statement command = new()
            {
                name = possibleFunctionName,

                line = possibleFunctionName.Position.Start.Line,

                startOffset = possibleFunctionName.Position.Start.Character,
                endOffset = possibleFunctionName.Position.End.Character,

                startOffsetTotal = possibleFunctionName.AbsolutePosition.Start,
                endOffsetTotal = possibleFunctionName.AbsolutePosition.End
            };

            possibleFunctionName.subtype = TokenSubtype.CommandName;

            if (paramCount > 0)
            {
                int endlessSafe = 0;
                while (ExpectLinebreak() == null)
                {
                    Token parameterInt = ExpectInt(out bool isNegative);
                    if (parameterInt == null)
                    {
                        Token parameterStr = ExpectString();
                        if (parameterStr == null)
                        {
                            Token parameterIdentify = ExpectIdentifier();
                            if (parameterIdentify == null)
                            {
                                throw new SyntaxException("Expected a parameter", possibleFunctionName);
                            }
                            else
                            {
                                parameterIdentify.subtype = TokenSubtype.LabelName;

                                command.identifyParameters.Add(parameterIdentify.text);

                                command.endOffset = parameterIdentify.Position.End.Character;
                                command.endOffsetTotal = parameterIdentify.AbsolutePosition.End;
                            }
                        }
                        else
                        {
                            command.strParameters.Add(parameterStr.text);

                            command.endOffset = parameterStr.Position.End.Character;
                            command.endOffsetTotal = parameterStr.AbsolutePosition.End;
                        }
                    }
                    else
                    {
                        if (isNegative == true)
                        {
                            command.intParameters.Add(int.Parse(parameterInt.text) * -1);
                        }
                        else
                        {
                            command.intParameters.Add(int.Parse(parameterInt.text));
                        }

                        command.endOffset = parameterInt.Position.End.Character;
                        command.endOffsetTotal = parameterInt.AbsolutePosition.End;
                    }

                    if (ExpectLinebreak() != null)
                    {
                        break;
                    }

                    if (ExpectOperator(",", out Token operatorVesszo) == null)
                    {
                        throw new SyntaxException("Expected ',' to separate parameters", parameterInt);
                    }

                    command.endOffset = operatorVesszo.Position.End.Character;
                    command.endOffsetTotal = operatorVesszo.AbsolutePosition.End;

                    endlessSafe++;
                    if (endlessSafe > 64) { throw new EndlessLoopException(); }
                }
            }

            return command;
        }

        Token ExpectIdentifier(string name)
        {
            if (CurrentToken == null) return null;
            if (CurrentToken.type != TokenType.IDENTIFIER) return null;
            if (name.Length > 0 && CurrentToken.text != name) return null;

            Token returnToken = CurrentToken;
            CurrentTokenIndex++;
            return returnToken;
        }
        Token ExpectIdentifier() => ExpectIdentifier("");
        Token ExpectOperator(string name, out Token outToken)
        {
            outToken = null;

            if (CurrentToken == null) return null;
            if (CurrentToken.type != TokenType.OPERATOR) return null;
            if (name.Length > 0 && CurrentToken.text != name) return null;

            Token returnToken = CurrentToken;
            outToken = CurrentToken;
            CurrentTokenIndex++;
            return returnToken;
        }

        Token ExpectLinebreak()
        {
            if (CurrentToken == null) return null;
            ConsumeComments();
            if (CurrentToken.type != TokenType.LINEBREAK) return null;

            Token returnToken = CurrentToken;
            CurrentTokenIndex++;
            return returnToken;
        }

        void ConsumeComments()
        {
            int endlessSafe = 0;
            while (true)
            {
                if (CurrentToken == null) break;
                if (CurrentToken.type != TokenType.COMMENT) break;
                CurrentTokenIndex++;

                endlessSafe++;
                if (endlessSafe > 16) throw new EndlessLoopException();
            }
        }
        void ConsumeLinebreaks()
        {
            int endlessSafe = 0;
            while (true)
            {
                if (CurrentToken == null) break;
                ConsumeComments();
                if (CurrentToken.type != TokenType.LINEBREAK) break;
                CurrentTokenIndex++;

                endlessSafe++;
                if (endlessSafe > 16) throw new EndlessLoopException();
            }
        }
    }
}