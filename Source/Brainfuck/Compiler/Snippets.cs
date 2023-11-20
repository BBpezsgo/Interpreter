#pragma warning disable IDE0059 // Unnecessary assignment of a value

namespace LanguageCore.Brainfuck
{
    public static class Snippets
    {
        #region LOGIC

        /// <summary>
        /// Result → <paramref name="addressX"/> (0 or 1)
        /// <br/>
        /// Pointer: <paramref name="addressX"/>
        /// <br/>
        /// <br/>
        /// Made by: me
        /// </summary>
        public static void LOGIC_MAKE_BOOL(this CompiledCode code, int addressX, int tempAddress1)
        {
            code.ClearValue(tempAddress1);
            code.JumpStart(addressX);
            code.AddValue(tempAddress1, 1);
            code.JumpEnd(addressX, true);
            code.MoveValue(tempAddress1, addressX);
            code.SetPointer(addressX);
        }

        /// <summary>
        /// Result → <paramref name="addressX"/> (0 or 1)
        /// <br/>
        /// Pointer: <paramref name="addressX"/>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#x%C2%B4_=_not_x_(boolean,_logical)"/>
        /// <br/>
        /// Attribution: Jeffry Johnston
        /// </summary>
        public static void LOGIC_NOT(this CompiledCode code, int addressX, int tempAddress1)
        {
            code.ClearValue(tempAddress1);
            code.JumpStart(addressX);
            code.AddValue(tempAddress1, 1);
            code.JumpEnd(addressX, true);
            code.AddValue(addressX, 1);
            code.JumpStart(tempAddress1);
            code.AddValue(addressX, -1);
            code.AddValue(tempAddress1, -1);
            code.JumpEnd(tempAddress1);
            code.SetPointer(addressX);
        }

        /// <summary>
        /// Result → <paramref name="addressX"/> (0 or 1)
        /// <br/>
        /// Pointer: <paramref name="tempAddress2"/>
        /// <br/>
        /// Preserves <paramref name="addressY"/>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#x%C2%B4_=_x_==_y"/>
        /// <br/>
        /// Attribution: Jeffry Johnston
        /// </summary>
        public static void LOGIC_EQ(this CompiledCode code, int addressX, int addressY, int tempAddress1, int tempAddress2)
        {
            code.ClearValue(tempAddress1);
            code.ClearValue(tempAddress2);

            code.MoveValue(addressX, tempAddress2);

            code.AddValue(addressX, 1);

            code.SetPointer(addressY);
            code.JumpStart();
            code.AddValue(tempAddress2, -1);
            code.AddValue(tempAddress1, 1);
            code.AddValue(addressY, -1);
            code.JumpEnd();

            code.MoveValue(tempAddress1, addressY);

            code.SetPointer(tempAddress2);
            code.JumpStart();
            code.AddValue(addressX, -1);
            code.ClearValue(tempAddress2);
            code.JumpEnd();

            code.ClearValue(tempAddress1);
            code.ClearValue(tempAddress2);
        }

        /// <summary>
        /// Result → <paramref name="addressX"/> (0 or 1)
        /// <br/>
        /// It does not preserve <paramref name="addressY"/>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#x%C2%B4_=_x_==_y"/>
        /// <br/>
        /// Attribution: Jeffry Johnston
        /// </summary>
        public static void LOGIC_EQ(this CompiledCode code, int addressX, int addressY)
        {
            // x[-y-x]
            code.SetPointer(addressX);
            code.JumpStart();
            code += '-';
            code.SetPointer(addressY);
            code += '-';
            code.SetPointer(addressX);
            code.JumpEnd();

            // +y
            code += '+';
            code.SetPointer(addressY);

            // [x-y[-]]
            code.JumpStart();
            code.SetPointer(addressX);
            code += '-';
            code.SetPointer(addressY);
            code.ClearCurrent();
            code.JumpEnd();
        }

        /// <summary>
        /// Result → <paramref name="addressX"/>
        /// <br/>
        /// Pointer: <paramref name="tempAddress2"/>
        /// <br/>
        /// Preserves <paramref name="addressY"/>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#x.C2.B4_.3D_x_.21.3D_y"/>
        /// <br/>
        /// Attribution: Jeffry Johnston
        /// </summary>
        public static void LOGIC_NEQ(this CompiledCode code, int addressX, int addressY, int tempAddress1, int tempAddress2)
        {
            /*
                temp0[-]
                temp1[-]
                x[temp1+x-]
                y[temp1-temp0+y-]
                temp0[y+temp0-]
                temp1[x+temp1[-]]
            */

            code.ClearValue(tempAddress1);
            code.ClearValue(tempAddress2);

            code.MoveValue(addressX, tempAddress2);

            code.SetPointer(addressY);
            code.JumpStart();
            code.AddValue(tempAddress2, -1);
            code.AddValue(tempAddress1, 1);
            code.AddValue(addressY, -1);
            code.JumpEnd();

            code.MoveValue(tempAddress1, addressY);
            code.SetPointer(tempAddress2);
            code.JumpStart();
            code.AddValue(addressX, 1);
            code.ClearValue(tempAddress2);
            code.JumpEnd();
        }

        /// <summary>
        /// Result → <paramref name="addressX"/> (0 or 1)
        /// <br/>
        /// Preserves <paramref name="addressY"/>
        /// <br/>
        /// <b>x and y are unsigned.</b>
        /// <br/>
        /// <b><paramref name="addressTemp2"/> is the first of three consecutive temporary cells.</b>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#x.C2.B4_.3D_x_.3C_y"/>
        /// <br/>
        /// Attribution: Ian Kelly
        /// </summary>
        public static void LOGIC_LT(this CompiledCode code, int addressX, int addressY, int addressTemp1, int addressTemp2)
        {
            code.ClearValue(addressTemp1);
            code.ClearValue(addressTemp2);

            code.SetValue(addressTemp2 + 1, 1);
            code.ClearValue(addressTemp2 + 2);

            code.CopyValueWithTemp(addressY, addressTemp1, addressTemp2);

            code.MoveValue(addressX, addressTemp1);
            code.AddValue(1);

            /*
                addressTemp2[>-]> [< x- addressTemp1[-] addressTemp2>->]<+<
                addressTemp1[addressTemp2- [>-]> [< x- addressTemp1[-]+ addressTemp2>->]<+< addressTemp1-]
             */

            code.SetPointer(addressTemp2);
            code += "[>-]>";
            code += "[<";

            code.AddValue(addressX, -1);

            code.ClearValue(addressTemp1);

            code.SetPointer(addressTemp2);

            code += ">->]<+<";

            code.SetPointer(addressTemp1);

            code.JumpStart();

            code.AddValue(addressTemp2, -1);

            code += "[>-]>";

            code += "[<";

            code.AddValue(addressX, -1);

            code.SetValue(addressTemp1, 1);

            code.SetPointer(addressTemp2);

            code += ">->]<+<";

            code.AddValue(addressTemp1, -1);
            code.JumpEnd();

            code.ClearValue(addressTemp1);
            code.ClearValue(addressTemp2);
            code.ClearValue(addressTemp2 + 1);

            code.SetPointer(addressX);
        }

        /// <summary>
        /// Result → <paramref name="addressX"/> (0 or 1)
        /// <br/>
        /// Pointer: <paramref name="addressX"/>
        /// <br/>
        /// Preserves <paramref name="addressY"/>
        /// <br/>
        /// <b>x and y are unsigned.</b>
        /// <br/>
        /// <b><paramref name="tempAddress2"/> is the first of three consecutive temporary cells.</b>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#x.C2.B4_.3D_x_.3C.3D_y"/>
        /// <br/>
        /// Attribution: Ian Kelly
        /// </summary>
        public static void LOGIC_LTEQ(this CompiledCode code, int addressX, int addressY, int tempAddress1, int tempAddress2)
        {
            code.ClearValue(tempAddress1, tempAddress2);
            code.SetValue(tempAddress2 + 1, 1);
            code.ClearValue(tempAddress2 + 2);

            code.MoveValue(addressY, tempAddress1, tempAddress2);
            code.MoveValue(tempAddress2, addressY);

            code.MoveValue(addressX, tempAddress2);

            code.SetPointer(tempAddress2);
            code += "[>-]>";

            code += "[<";
            code.AddValue(addressX, 1);
            code.ClearValue(tempAddress1);
            code.SetPointer(tempAddress2);

            code += ">->]<+<";

            code.SetPointer(tempAddress1);
            code.JumpStart();
            code.AddValue(tempAddress2, -1);
            code += "[>-]>";

            code += "[<";

            code.AddValue(addressX, 1);

            code.SetValue(tempAddress1, 1);

            code.SetPointer(tempAddress2);

            code += ">->]<+<";

            code.AddValue(tempAddress1, -1);
            code.JumpEnd();

            code.ClearValue(tempAddress1);
            code.ClearValue(tempAddress2);
            code.ClearValue(tempAddress2 + 1);

            code.SetPointer(addressX);
        }

        /// <summary>
        /// Result → <paramref name="addressResult"/> (0 or 1)
        /// <br/>
        /// Pointer: <paramref name="addressResult"/>
        /// <br/>
        /// Does not preserves <paramref name="addressX"/> and <paramref name="addressY"/>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#z_.3D_x_.3E_y"/>
        /// <br/>
        /// Attribution: User:ais523
        /// <para>
        /// This uses balanced loops only,
        /// and <b>requires a wrapping implementation</b>
        /// (and will be very slow with large numbers of bits,
        /// although the number of bits otherwise doesn't matter.)
        /// The temporaries and x are left at 0; y is set to y-x.
        /// (You could make a temporary copy of x via using another temporary
        /// that's incremented during the loop.)
        /// </para>
        /// </summary>
        public static void LOGIC_MT(this CompiledCode c, int addressX, int addressY, int addressResult, int tempAddress1, int tempAddress2)
        {
            c.ClearValue(tempAddress1);
            c.ClearValue(tempAddress2);
            c.ClearValue(addressResult);

            c.SetPointer(addressX);
            c += '[';

            c.AddValue(tempAddress1, 1);

            c.SetPointer(addressY);

            c += "[-";

            c.ClearValue(tempAddress1);

            c.AddValue(tempAddress2, 1);

            c.SetPointer(addressY);
            c += ']';

            c.SetPointer(tempAddress1);
            c += "[-";
            c.AddValue(addressResult, 1);
            c.SetPointer(tempAddress1);
            c += ']';

            c.SetPointer(tempAddress2);
            c += "[-";
            c.AddValue(addressY, 1);
            c.SetPointer(tempAddress2);
            c += ']';

            c.AddValue(addressY, -1);
            c.AddValue(addressX, -1);

            c += ']';

            c.ClearValue(addressY);
            c.SetPointer(addressResult);
        }

        /// <summary>
        /// Result → <paramref name="addressX"/> (0 or 1)
        /// <br/>
        /// Pointer: <paramref name="tempAddress2"/>
        /// <br/>
        /// Preserves <paramref name="addressY"/>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#z_.3D_x_.3E_y"/>
        /// </summary>
        public static void LOGIC_AND(this CompiledCode code, int addressX, int addressY, int tempAddress1, int tempAddress2)
        {
            /*
                temp1[
                 temp1[-]
                 y[temp1+temp0+y-]
                 temp0[y+temp0-]
                 temp1[x+temp1[-]]
                ]
             */

            code.ClearValue(tempAddress1);
            code.ClearValue(tempAddress2);

            code.MoveValue(addressX, tempAddress2);

            code.JumpStart(tempAddress2);

            code.MoveValue(addressY, tempAddress2, tempAddress1);

            code.MoveValue(tempAddress1, addressY);

            code.JumpStart(tempAddress2);
            code.AddValue(addressX, 1);
            code.ClearValue(tempAddress2);
            code.JumpEnd();
            code.JumpEnd();
        }

        /// <summary>
        /// Result → <paramref name="addressX"/> (0 or 1)
        /// <br/>
        /// Pointer: <paramref name="addressX"/>
        /// <br/>
        /// Does not preserves <paramref name="addressY"/>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#z_.3D_x_or_y_.28boolean.2C_logical.29_.28wrapping.29"/>
        /// </summary>
        public static void LOGIC_OR(this CompiledCode code, int addressX, int addressY, int tempStart)
        {
            code.ClearValue(tempStart + 0);
            code.ClearValue(tempStart + 1);
            code.ClearValue(tempStart + 2);
            code.ClearValue(tempStart + 3);

            code.MoveValue(addressX, tempStart + 2);
            code.MoveValue(addressY, tempStart + 3);
            code.SetPointer(tempStart + 2);

            code += "[<<->]<+[<]+>[-<->>+>[<-<<+>>]<[-<]]>";

            code.ClearValue(tempStart + 3);
            code.ClearValue(tempStart + 2);
            code.ClearValue(tempStart + 1);
            code.MoveValue(tempStart + 0, addressX);
            code.SetPointer(addressX);
        }

        #endregion

        #region MATH

        /// <summary>
        /// Result → <paramref name="addressX"/>
        /// <br/>
        /// Pointer: <paramref name="addressTemp2"/>
        /// <br/>
        /// Preserves <paramref name="addressY"/>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#x.C2.B4_.3D_x_.2A_y"/>
        /// </summary>
        public static void MULTIPLY(this CompiledCode code, int addressX, int addressY, int addressTemp1, int addressTemp2)
        {
            code.ClearValue(addressTemp1, addressTemp2);

            code.SetPointer(addressX);

            code.JumpStart();

            code.SetPointer(addressTemp2);
            code += '+';
            code.SetPointer(addressX);
            code += '-';
            code.JumpEnd();

            code.SetPointer(addressTemp2);
            code.JumpStart();
            code.SetPointer(addressY);
            code.JumpStart();
            code.SetPointer(addressX);
            code += '+';
            code.SetPointer(addressTemp1);
            code += '+';
            code.SetPointer(addressY);
            code += '-';
            code.JumpEnd();
            code.SetPointer(addressTemp1);
            code.JumpStart();
            code.SetPointer(addressY);
            code += '+';
            code.SetPointer(addressTemp1);
            code += '-';
            code.JumpEnd();
            code.SetPointer(addressTemp2);
            code += '-';
            code.JumpEnd();
        }

        /// <summary>
        /// Result → <paramref name="addressX"/>
        /// <br/>
        /// Pointer: <paramref name="addressTemp1"/>
        /// <br/>
        /// Preserves <paramref name="addressY"/>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#x%C2%B4_=_x_/_y"/>
        /// <br/>
        /// Attribution: Jeffry Johnston
        /// </summary>
        public static void MATH_DIV(this CompiledCode code, int addressX, int addressY, int addressTemp1, int addressTemp2, int addressTemp3, int addressTemp4)
        {
            code.ClearValue(addressTemp1, addressTemp2, addressTemp3, addressTemp4);

            code.MoveValue(addressX, addressTemp1);

            code.JumpStart(addressTemp1);

            code.MoveValue(addressY, addressTemp2, addressTemp3);
            code.MoveValue(addressTemp3, addressY);

            code.JumpStart(addressTemp2);

            code.SetPointer(addressTemp3);
            code += '+';

            code.SetPointer(addressTemp1);
            code += '-';
            code.JumpStart();

            code.ClearValue(addressTemp3);
            code.SetPointer(addressTemp4);
            code += '+';
            code.SetPointer(addressTemp1);
            code += '-';
            code.JumpEnd();

            code.MoveValue(addressTemp4, addressTemp1);

            code.SetPointer(addressTemp3);
            code.JumpStart();

            code.SetPointer(addressTemp2);
            code += '-';

            code.JumpStart();
            code.SetPointer(addressX);
            code += '-';
            code.ClearValue(addressTemp2);
            code.SetPointer(addressTemp2);
            code.JumpEnd();
            code += '+';

            code.SetPointer(addressTemp3);
            code += '-';
            code.JumpEnd();

            code.SetPointer(addressTemp2);
            code += '-';
            code.JumpEnd();

            code.SetPointer(addressX);
            code += '+';

            code.SetPointer(addressTemp1);
            code.JumpEnd();
        }

        /// <summary>
        /// Result → <paramref name="addressX"/>
        /// <br/>
        /// Pointer: <paramref name="addressTemp1"/>
        /// <br/>
        /// It does not preserve <paramref name="addressY"/>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#x%C2%B4_=_x_%5E_y"/>
        /// <br/>
        /// Attribution: chad3814
        /// </summary>
        public static void MATH_POW(this CompiledCode code, int addressX, int addressY, int addressTemp1, int addressTemp2, int addressTemp3)
        {
            code.ClearValue(addressTemp1);

            code.MoveValue(addressX, addressTemp1);

            code.SetPointer(addressX);
            code += '+';

            code.SetPointer(addressY);
            code.JumpStart();

            code.ClearValue(addressTemp2);

            code.MoveValue(addressX, addressTemp3);

            code.SetPointer(addressTemp3);
            code.JumpStart();

            code.SetPointer(addressTemp1);
            code.JumpStart();
            code.SetPointer(addressX);
            code += '+';
            code.SetPointer(addressTemp2);
            code += '+';
            code.SetPointer(addressTemp1);
            code += '-';
            code.JumpEnd();

            code.MoveValue(addressTemp2, addressTemp1);

            code.SetPointer(addressTemp3);
            code += '-';
            code.JumpEnd();

            code.SetPointer(addressY);
            code += '-';
            code.JumpEnd();

            code.ClearValue(addressTemp1);
        }

        /// <summary>
        /// Pointer: <paramref name="addressX"/>
        /// </summary>
        public static void MATH_MOD(this CompiledCode code, int addressX, int addressY, int free6CellStart)
        {
            /*
                # 0 >n d 0 0 0
                [>->+<[>]>[<+>-]<<[<]>-]
                # 0 >0 d-n%d n%d 0 0
            */

            code.MoveValue(addressX, free6CellStart + 1);
            code.MoveValue(addressY, free6CellStart + 2);
            code.SetPointer(free6CellStart + 1);
            code += "[>->+<[>]>[<+>-]<<[<]>-]";
            code.ClearValue(free6CellStart + 2);
            code.MoveValue(free6CellStart + 3, addressX);
            code.SetPointer(addressX);
        }

        /// <summary>
        /// Pointer: <paramref name="tempAddress1"/>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#x.C2.B4_.3D_x_.2A_x"/>
        /// <br/>
        /// Attribution: Softengy (talk) 15:44, 7 April 2020 (UTC)
        /// </summary>
        public static void MATH_MUL_SELF(this CompiledCode code, int addressX, int tempAddress1, int tempAddress2)
        {
            // x[temp0+x-]

            code.SetPointer(addressX);
            code.JumpStart();
            code.SetPointer(tempAddress1);
            code += '+';
            code.SetPointer(addressX);
            code += '-';
            code.JumpEnd();

            // temp0[-[temp1+x++temp0-]x+temp1[temp0+temp1-]temp0]

            code.SetPointer(tempAddress1);
            code += "[-[";

            code.SetPointer(tempAddress2);
            code += '+';
            code.SetPointer(addressX);
            code += "++";
            code.SetPointer(tempAddress1);
            code += "-]";
            code.SetPointer(addressX);
            code += '+';
            code.SetPointer(tempAddress2);
            code.JumpStart();
            code.SetPointer(tempAddress1);
            code += '+';
            code.SetPointer(tempAddress2);
            code += "-]";
            code.SetPointer(tempAddress1);
            code.JumpEnd();
        }

        /// <summary>
        /// Pointer: <paramref name="tempAddress"/>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#x_.3D_-x"/>
        /// </summary>
        public static void SWAP_SIGN(this CompiledCode code, int addressX, int tempAddress)
        {
            code.ClearValue(tempAddress);

            code.JumpStart(addressX);
            code.SetPointer(tempAddress);
            code += '-';
            code.SetPointer(addressX);
            code += "-]";

            code.SetPointer(tempAddress);
            code.JumpStart();
            code.SetPointer(addressX);
            code += '-';
            code.SetPointer(tempAddress);
            code += '+';
            code.JumpEnd();
        }

        #endregion

        #region UTILITIES

        /// <summary>
        /// Uses <paramref name="x"/> and two cells to the right of it.
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#Non-wrapping"/>
        /// <br/>
        /// Attribution: <see href="https://esolangs.org/wiki/User:JHM">JungHwan Min</see>
        /// <br/>
        /// Originally posted at <see href="https://codegolf.stackexchange.com/a/118441/60043">Code Golf Stack Exchange</see>
        /// </summary>
        public static void UTIL_CLEAR_NW(this CompiledCode code, int x)
        {
            code.SetPointer(x);
            code += "> [-]";
            code += "> [-]";
            code += "<<";
            code += "[";
            code += "> +[-< +>> +<]";
            code += "< [>] >";
            code += "[";
            code += "+[-< +<->>]";
            code += "< [->+<]";
            code += "]";
            code += "> [-< +>]";
            code += "<<";
            code += "]";
            code += "> [-] <";
        }

        /// <summary>
        /// Pointer: <paramref name="tempAddress"/>
        /// </summary>
        public static void SWAP(this CompiledCode code, int addressX, int addressY, int tempAddress)
        {
            code.MoveValue(addressX, tempAddress);
            code.MoveValue(addressY, addressX);
            code.MoveValue(tempAddress, addressY);
        }

        /// <summary>
        /// <b>Pointer:</b> <paramref name="switchAddress"/>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#z_=_MUX(a,_x,_y)_(boolean,_logical)"/>
        /// <br/>
        /// Attribution: Yuval Meshorer
        /// <para>
        /// If <paramref name="switchAddress"/> is equal to <c>1</c>, then <paramref name="resultAddress"/> is equal to <paramref name="valueAddressB"/>. Otherwise, if <paramref name="switchAddress"/> is equal to <c>0</c>, <paramref name="resultAddress"/> will
        /// be equal to <paramref name="valueAddressA"/>. When done, <paramref name="switchAddress"/>, <paramref name="valueAddressA"/>, and <paramref name="valueAddressB"/> will all be <c>0</c> regardless of their starting
        /// values.
        /// </para>
        /// <para>
        /// e.g:
        /// <br/>
        /// IN: <paramref name="valueAddressA"/> = <c>0</c>, <paramref name="valueAddressB"/> = <c>1</c>, <paramref name="switchAddress"/> = <c>1</c>
        /// <br/>
        /// OUT: <paramref name="valueAddressA"/> = <c>0</c>, <paramref name="valueAddressB"/> = <c>0</c>, <paramref name="switchAddress"/> = <c>0</c>, <paramref name="resultAddress"/> = <c>1</c>
        /// </para>
        /// </summary>
        public static void MUX(this CompiledCode code, int switchAddress, int valueAddressA, int valueAddressB, int resultAddress)
        {
            // code.ClearValue(resultAddress);

            code.SetPointer(valueAddressB);
            code.JumpStart();
            code.MoveValue(switchAddress, resultAddress);
            code.AddValue(valueAddressB, -1);
            code.JumpEnd();

            code.SetPointer(valueAddressA);
            code.JumpStart();

            code.AddValue(switchAddress, -1);
            code.JumpStart();
            code.ClearCurrent();
            code.SetValue(resultAddress, 1);
            code.SetPointer(switchAddress);
            code.JumpEnd();

            code.AddValue(valueAddressA, -1);
            code.JumpEnd();

            code.ClearValue(switchAddress);
        }

        #endregion

        #region BITS

        /// <summary>
        /// Pointer: <paramref name="tempAddress"/>
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#x.C2.B4_.3D_not_x_.28bitwise.29"/>
        /// </summary>
        public static void BITS_NOT(this CompiledCode code, int addressX, int tempAddress)
        {
            code.ClearValue(tempAddress);

            code.AddValue(addressX, -1);
            code.JumpStart();
            code.AddValue(tempAddress, -1);
            code.AddValue(addressX, -1);
            code.JumpEnd();

            code.MoveValue(tempAddress, addressX);

            /*
                temp0[-]
                x-
                [temp0-x-]
                temp0[x+temp0-]
            */
        }

        #endregion

        /*
        public static string Helper(string raw)
        {
            char[] codeChars = new char[]
            {
                '>', '<',
                '+', '-',
                '[', ']',
                ',', '.',
            };
            char[] whitespace = new char[]
            {
                ' ', '\0',
                '\t', '\n',
                '\r',
            };

            List<string> usedVariables = new();
            string currentVariable = "";

            for (int i = 0; i < raw.Length; i++)
            {
                var c = raw[i];
                if (codeChars.Contains(c) || whitespace.Contains(c))
                {
                    if (!string.IsNullOrEmpty(currentVariable))
                    {
                        if (!usedVariables.Contains(currentVariable))
                        {
                            usedVariables.Add(currentVariable);
                        }
                        currentVariable = "";
                    }
                    continue;
                }
                currentVariable += c;
            }

            string result = "public static void SNIPPET(this CompiledCode c";
            foreach (string v in usedVariables)
            {
                result += ", int " + v;
            }
            result += ")\n{\n";

            {
                int i = -1;
                while (++i < raw.Length)
                {
                    char c = raw[i];
                    if (whitespace.Contains(c)) continue;
                    if (codeChars.Contains(c))
                    {
                        result += $"c += \"{c}\";\n";
                        continue;
                    }

                    string _currentVariable = "";
                    while (i < raw.Length)
                    {
                        if (whitespace.Contains(raw[i])) break;
                        if (codeChars.Contains(raw[i])) break;
                        _currentVariable += raw[i];
                        i++;
                    }
                    i--;
                    result += $"c.SetPointer({_currentVariable});\n";
                }
            }

            result += "}";

            return result;
        }
        */

        #region ARRAY

        public static int ARRAY_SIZE(int elementCount) => (2 * elementCount) + 3;
        /// <summary>
        /// <b>Pointer:</b> <paramref name="arrayAddress"/>
        /// <br/>
        /// <br/>
        /// <see href="https://code.google.com/archive/p/brainfuck-compiler/"/>
        /// <br/>
        /// Attribution: Jeffry Johnston
        /// <para>
        /// Each array element requires 2 memory cells.
        /// </para>
        /// </summary>
        public static void ARRAY_SET(this CompiledCode code, int arrayAddress, int indexAddress, int valueAddress, int temp0)
        {
            int temp1 = arrayAddress + 1;
            int temp2 = arrayAddress + 2;

            code.ClearValue(arrayAddress + 1, temp2, temp0);

            code.CopyValueWithTemp(indexAddress, temp0, temp2);
            code.CopyValueWithTemp(valueAddress, temp0, temp1);

            code.SetPointer(arrayAddress);

            code += ">>";
            code += "[[>>] + [<<] >> -]";
            code += '+';
            code += "[>>] < [-] < [<<]";
            code += "> [> [>>] < +< [<<] > -]";
            code += "> [>>] << [-<<]";
        }

        public static void ARRAY_SET_CONST(this CompiledCode code, int arrayAddress, int index, Runtime.DataItem value)
        {
            index *= 2;
            index += 3;
            arrayAddress += index;
            code.SetValue(arrayAddress, value);
        }

        /// <summary>
        /// <b>Pointer:</b> <paramref name="arrayAddress"/>
        /// <br/>
        /// <br/>
        /// <see href="https://code.google.com/archive/p/brainfuck-compiler/"/>
        /// <br/>
        /// Attribution: Jeffry Johnston
        /// <para>
        /// Each array element requires 2 memory cells.
        /// </para>
        /// </summary>
        public static void ARRAY_GET(this CompiledCode code, int arrayAddress, int indexAddress, int resultAddress)
        {
            code.ClearValue(resultAddress, arrayAddress + 1, arrayAddress + 2);
            code.CopyValueWithTemp(indexAddress, arrayAddress + 1, arrayAddress + 2);

            code.SetPointer(arrayAddress);
            code += ">> [[>>] + [<<] >> -] + [>>] < [< [<<] > +<"; // pointer is at y
            code.FixPointer(arrayAddress);
            code.SetPointer(resultAddress);
            code += '+';
            code.SetPointer(arrayAddress);
            code += ">> [>>] < -] < [<<] > [> [>>] < +< [<<] > -] > [>>] << [-<<]";
            code.FixPointer(arrayAddress);
        }

        #endregion

        #region OUT

        /// <summary>
        /// <b>Cells used:</b> <c>V Z n d 1 0 0 0</c>
        /// <br/>
        /// V is the value you need to print; it is not modified
        /// <br/>
        /// Z is a zero sentinal and tmp
        /// <br/>
        /// All cells Z and up are cleared by this routine
        /// <br/>
        /// <br/>
        /// <see href="https://esolangs.org/wiki/brainfuck_algorithms#Print_value_of_cell_x_as_number_for_ANY_sized_cell_(eg_8bit,_100000bit_etc)"/>
        /// </summary>
        public const string OUT_AS_STRING =
            ">[-] >[-] +>[-] +<" +
            "[" +
            ">[-< -" +
            "<<[->+> +<<]" +
            ">[-< +>] >>" +
            "]" +
            "++++++++++ >[-] +>[-] >[-] >[-] <<<<<" +
            "[->-[> +>>] >[[-< +>] +> +>>] <<<<<]" +
            ">> -[-<< +>>]" +
            "<[-]++++++++[-< ++++++>]" +
            ">>[-<< +>>]" +
            "<<" +
            "" +
            "]" +
            "<[.[-]<]" +
            "" +
            "<";

        public const string OUT_AS_STRING_8BIT =
            ">>++++++++++<<[->+>-[>+>>]>[+[-<+>]>+>>]<<<<<<]>>[-]>>>++++++++++<[->-[>+>>]>[+[-" +
            "<+>]>+>>]<<<<<]>[-]>>[>++++++[-<++++++++>]<.<<+>+>[-]]<[<[->-<]++++++[->++++++++" +
            "<]>.[-]]<<++++++[-<++++++++>]<.[-]<<[-<+>]<";

        /// <summary>
        /// <b>Pointer:</b> <paramref name="tempAddress"/>
        /// </summary>
        /// <param name="code"></param>
        /// <param name="tempAddress"></param>
        /// <param name="message"></param>
        public static void OUT_STRING(this CompiledCode code, int tempAddress, string message)
        {
            code.ClearValue(tempAddress);

            byte prevValue = 0;
            for (int i = 0; i < message.Length; i++)
            {
                code.SetPointer(tempAddress);
                byte charToPrint = CharCode.GetByte(message[i]);

                while (prevValue > charToPrint)
                {
                    code += '-';
                    prevValue--;
                }

                while (prevValue < charToPrint)
                {
                    code += '+';
                    prevValue++;
                }

                prevValue = charToPrint;

                code += '.';
            }

            code.ClearValue(tempAddress);
        }

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        public static void OUT_STRING_UNSAFE(this CompiledCode code, int tempOffset, string message)
        {
            code.MovePointerUnsafe(tempOffset);
            code.ClearCurrent();

            byte prevValue = 0;
            for (int i = 0; i < message.Length; i++)
            {
                byte charToPrint = CharCode.GetByte(message[i]);

                while (prevValue > charToPrint)
                {
                    code += '-';
                    prevValue--;
                }

                while (prevValue < charToPrint)
                {
                    code += '+';
                    prevValue++;
                }

                prevValue = charToPrint;

                code += '.';
            }

            code.ClearCurrent();
            code.MovePointerUnsafe(-tempOffset);
        }

        #endregion
    }
}
