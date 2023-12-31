using System.Text;

namespace LanguageCore.Brainfuck
{
    /// <summary>
    /// Source: <see href="https://esolangs.org/wiki/Brainfuck_bitwidth_conversions"/>
    /// </summary>
    public static class BitwidthConversions
    {
        public static string To16(string code8)
        {
            StringBuilder builder = new();
            builder.Append('>');

            for (int i = 0; i < code8.Length; i++)
            {
                char c = code8[i];
                switch (c)
                {
                    case '.':
                        builder.Append('.');
                        break;
                    case ',':
                        builder.Append(',');
                        break;
                    case '>':
                        builder.Append(">>>");
                        break;
                    case '<':
                        builder.Append("<<<");
                        break;
                    case '+':
                        builder.Append("+[<+>>>+<<-]<[>+<-]+>>>[<<<->>>[-]]<<<[->>+<<]>");
                        break;
                    case '-':
                        builder.Append("[<+>>>+<<-]<[>+<-]+>>>[<<<->>>[-]]<<<[->>-<<]>-");
                        break;
                    case '[':
                        builder.Append("[>>+>>>+<<<<<-]>>>>>[<<<<<+>>>>>-]<<<[[-]<<<+>>>]<[>+>>>+<<<<-]>>>>[<<<<+>>>>-]<<<[[-]<<<+>>>]<<<[[-]>");
                        break;
                    case ']':
                        builder.Append("[>>+>>>+<<<<<-]>>>>>[<<<<<+>>>>>-]<<<[[-]<<<+>>>]<[>+>>>+<<<<-]>>>>[<<<<+>>>>-]<<<[[-]<<<+>>>]<<<]>");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }

        public static string To32(string code8)
        {
            StringBuilder builder = new();
            builder.Append('>');

            for (int i = 0; i < code8.Length; i++)
            {
                char c = code8[i];
                switch (c)
                {
                    case '.':
                        builder.Append('.');
                        break;
                    case ',':
                        builder.Append(',');
                        break;
                    case '>':
                        builder.Append(">>>>>");
                        break;
                    case '<':
                        builder.Append("<<<<<");
                        break;
                    case '+':
                        builder.Append("+[<+>>>>>+<<<<-]<[>+<-]+>>>>>[<<<<<->>>>>[-]]<<<<<[->>+[<<+>>>>>+<<<-]<<[>>+<<-]+>>>>>[<<<<<->>>>>[-]]<<<<<[->>>+[<<<+>>>>>+<<-]<<<[>>>+<<<-]+>>>>>[<<<<<->>>>>[-]]<<<<<[->>>>+<<<<]]]>");
                        break;
                    case '-':
                        builder.Append("[<+>>>>>+<<<<-]<[>+<-]+>>>>>[<<<<<->>>>>[-]]<<<<<[->>[<<+>>>>>+<<<-]<<[>>+<<-]+>>>>>[<<<<<->>>>>[-]]<<<<<[->>>[<<<+>>>>>+<<-]<<<[>>>+<<<-]+>>>>>[<<<<<->>>>>[-]]<<<<<[->>>>-<<<<]>>>-<<<]>>-<<]>-");
                        break;
                    case '[':
                        builder.Append("[>>>>+>>>>>+<<<<<<<<<-]>>>>>>>>>[<<<<<<<<<+>>>>>>>>>-]<<<<<[[-]<<<<<+>>>>>]<<<[>>>+>>>>>+<<<<<<<<-]>>>>>>>>[<<<<<<<<+>>>>>>>>-]<<<<<[[-]<<<<<+>>>>>]<<[>>+>>>>>+<<<<<<<-]>>>>>>>[<<<<<<<+>>>>>>>-]<<<<<[[-]<<<<<+>>>>>]<[>+>>>>>+<<<<<<-]>>>>>>[<<<<<<+>>>>>>-]<<<<<[[-]<<<<<+>>>>>]<<<<<[[-]>");
                        break;
                    case ']':
                        builder.Append("[>>>>+>>>>>+<<<<<<<<<-]>>>>>>>>>[<<<<<<<<<+>>>>>>>>>-]<<<<<[[-]<<<<<+>>>>>]<<<[>>>+>>>>>+<<<<<<<<-]>>>>>>>>[<<<<<<<<+>>>>>>>>-]<<<<<[[-]<<<<<+>>>>>]<<[>>+>>>>>+<<<<<<<-]>>>>>>>[<<<<<<<+>>>>>>>-]<<<<<[[-]<<<<<+>>>>>]<[>+>>>>>+<<<<<<-]>>>>>>[<<<<<<+>>>>>>-]<<<<<[[-]<<<<<+>>>>>]<<<<<]>");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
