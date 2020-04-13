using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using Debug = System.Console;

namespace CHIP.NET
{
    enum CompatibilitySettings {
        LOAD_STORE,
        SHIFT
    }
    class Emulator
    {
        ushort opcode;
        int nibble;
        byte[] memory = new byte[4096];
        byte[] registers = new byte[16];
        ushort idx_reg;
        bool[,] gfx = new bool[64, 32];
        byte delay_timer;
        byte sound_timer;
        ushort[] stack = new ushort[255];
        byte stack_ptr;
        ushort font_ptr;
        ushort input;
        Random randomGenerator = new Random();
        int elapsed;
        bool[] compat_settings;
        public bool beepFlag;
        bool wait_flag;
        int wait_reg;
        public Emulator(byte[] raw, byte[] font, ushort font_pointer = 0x0) {
            compat_settings = new bool[Enum.GetNames(typeof(CompatibilitySettings)).Length];
            font_ptr = font_pointer;
            font.CopyTo(memory, font_ptr); //Mount font at pointer
            raw.CopyTo(memory, 0x200); //Mount program
            stack[0] = 0x200;
        }
        public void EnableCompat(CompatibilitySettings setting) {
            compat_settings[(int)setting] = true;
        }
        public bool Step(ushort hex_input, int deltaInMilliseconds) {
            input = hex_input;
            beepFlag = false;
            if (wait_flag) {
                if (hex_input > 0) {
                    for (byte i = 0;i <= 0xF && wait_flag;i++) {
                        if ((hex_input & (1 << i)) > 0) {
                            registers[wait_reg] = i;
                            wait_flag = false;
                        }
                    }
                } else {
                    return false;
                }
            }
            elapsed += deltaInMilliseconds;
            
            if (elapsed * 60 > 1000) {
                if (delay_timer > 0) --delay_timer;
                if (sound_timer > 0) {
                    beepFlag = --sound_timer <= 0;
                }
                elapsed = 0;
            }
            opcode = ReadOpcode();
            nibble = opcode & 0xF000;
            Debug.WriteLine("Current opcode: " + opcode.ToString("X"));

            switch (nibble) { //Only take first four bits
                case 0x0000: //Clears the screen / Returns from a subroutine
                    ClearScreenOrReturn();
                    break;

                case 0x1000: //Jumps to address
                    JumpTo();
                    break;

                case 0x2000: //Call subroutine
                    SubroutineAt();
                    break;

                case 0x3000:
                case 0x4000:
                    SkipIfEqualOrNot();
                    break;

                case 0x5000:
                    if ((opcode & 0x000F) != 0) break;
                    SkipIfRegisterEqualOrNot();
                    break;

                case 0x6000:
                case 0x7000:
                    AddOrSetRegister();
                    break;

                case 0x8000:
                    RegisterMath();
                    break;

                case 0x9000:
                    if ((opcode & 0x000F) != 0) break;
                    SkipIfRegisterEqualOrNot();
                    break;

                case 0xA000:
                    SetIdxReg();
                    break;

                case 0xB000:
                    JumpTo(registers[0]);
                    break;

                case 0xC000:
                    RandomiseRegister();
                    break;

                case 0xD000:
                    DrawSprite();
                    break;

                case 0xE000:
                    SkipIfKeyOrNot();
                    break;

                case 0xF000:
                    MiscOpcodes();
                    break;
                default:
                    Debug.WriteLine("Unimplemented or invalid opcode!");
                    break;
            }

            return stack[stack_ptr] >= memory.Length;
        }
        ushort ReadOpcode() {
            ushort pointer = stack[stack_ptr];
            stack[stack_ptr] += 2;
            return BitConverter.ToUInt16(new byte[] {memory[pointer + 1], memory[pointer] }, 0);
        }

        //Opcodes
        void ClearScreenOrReturn() {
            if (opcode == 0x00E0)
            { //Clears the screen
                Array.Clear(gfx, 0, gfx.Length);
                Debug.WriteLine("Cleared screen.");
            }
            else if (opcode == 0x00EE)
            { //Returns from a subroutine
                if (stack_ptr > 0)
                {
                    --stack_ptr;
                }
            }
        }
        void JumpTo(ushort plus = 0) {
            stack[stack_ptr] = (ushort)(opcode & 0x0FFF); //Read address
            stack[stack_ptr] += plus;
            Debug.WriteLine("Jumped to " + stack[stack_ptr].ToString("X"));
        }
        void SubroutineAt() {
            if (++stack_ptr < stack.Length)
            {
                stack[stack_ptr] = (ushort)(opcode & 0x0FFF); //Read address
                Debug.WriteLine("Called subroutine at " + stack[stack_ptr].ToString("X"));
            }
            else
            {
                throw new StackOverflowException("Emulator stack overflow!");
            }
        }
        void RandomiseRegister() {
            int reg = (opcode & 0x0F00) >> 8;
            int b = opcode & 0x00FF;

            byte[] random = new byte[1];
            randomGenerator.NextBytes(random);
            //Console.WriteLine(random[]);
            registers[reg] = (byte)(b & random[0]);

            Debug.WriteLine("Set register V" + reg + " to " + registers[reg].ToString("X") + ".");
        }
        void SkipIfEqualOrNot() {
            Debug.WriteLine(
                        "Comparing " +
                        registers[(opcode & 0x0F00) >> 8].ToString() +
                        " with " +
                        (opcode & 0x00FF).ToString() +
                        "."
                    );
            Debug.WriteLine((registers[(opcode & 0x0F00) >> 8] == (opcode & 0x00FF)) == (nibble == 0x3000));
            if ((registers[(opcode & 0x0F00) >> 8] == (opcode & 0x00FF)) == (nibble == 0x3000))
            {
                Debug.WriteLine("Skipped instruction.");
                stack[stack_ptr] += 2;
            }
        }
        void SkipIfRegisterEqualOrNot() {
            Debug.WriteLine(
                "Comparing " +
                registers[(opcode & 0x0F00) >> 8].ToString("X") +
                " with " +
                registers[(opcode & 0x00F0) >> 4].ToString("X") +
                "."
            );
            if ((registers[(opcode & 0x0F00) >> 8] == registers[(opcode & 0x00F0) >> 4]) == (nibble == 0x5000))
            {
                Debug.WriteLine("Skipped instruction.");
                stack[stack_ptr] += 2;
            }
        }
        void SkipIfKeyOrNot() {
            if (((input & (1 << registers[(opcode & 0x0F00) >> 8])) > 0) == ((opcode & 0x00FF) == 0x9E))
            {
                stack[stack_ptr] += 2;
            }
        }

        void AddOrSetRegister() {
            int reg = (opcode & 0x0F00) >> 8;
            byte c = (byte)(opcode & 0x00FF);
            if (nibble == 0x6000)
            {
                Debug.WriteLine("Set V" + reg + " to " + c + ".");
                registers[reg] = c;
            }
            else
            {
                Debug.WriteLine("Added " + c + " to V" + reg + ".");
                registers[reg] += c;
            }
        }
        void RegisterMath() {
            int reg1 = (opcode & 0x0F00) >> 8;
            int reg2 = (opcode & 0x00F0) >> 4;
            bool shift_quirk = compat_settings[(int)CompatibilitySettings.SHIFT];
            switch (opcode & 0x000F)
            {
                case 0x0:
                    registers[reg1] = registers[reg2];
                    Debug.WriteLine("Set V" + reg1 + " to " + registers[reg2] + ".");
                    break;
                case 0x1:
                    registers[reg1] = (byte)(registers[reg1] | registers[reg2]);
                    break;
                case 0x2:
                    registers[reg1] = (byte)(registers[reg1] & registers[reg2]);
                    break;
                case 0x3:
                    registers[reg1] = (byte)(registers[reg1] ^ registers[reg2]);
                    break;
                case 0x4:
                    registers[15] = (byte)((registers[reg1] + registers[reg2]) > 255 ? 1 : 0);
                    registers[reg1] += registers[reg2];
                    break;
                case 0x5:
                    registers[15] = (byte)((registers[reg1] - registers[reg2]) < 0 ? 0 : 1);
                    registers[reg1] -= registers[reg2];
                    break;
                case 0x6:
                    registers[15] = (byte)((registers[shift_quirk ? reg1 : reg2] & 0b00000001) != 0 ? 1 : 0);
                    registers[reg1] = (byte)(registers[shift_quirk ? reg1 : reg2] >> 1);
                    break;
                case 0x7:
                    registers[15] = (byte)((registers[reg1] - registers[reg2]) < 0 ? 1 : 0);
                    registers[reg1] = (byte)(registers[reg2] - registers[reg1]);
                    break;
                case 0xE:
                    registers[15] = (byte)((registers[shift_quirk ? reg1 : reg2] & 0b10000000) != 0 ? 1 : 0);
                    registers[reg1] = (byte)(registers[shift_quirk ? reg1 : reg2] << 1);
                    break;
            }
        }
        void SetIdxReg() {
            idx_reg = (ushort)(opcode & 0x0FFF); //Read address
            Debug.WriteLine("Set index register to " + idx_reg.ToString("X"));
        }
        void DrawSprite() {
            //Console.WriteLine(opcode.ToString("X"));
            byte x = registers[(opcode & 0x0F00) >> 8];
            byte y = registers[(opcode & 0x00F0) >> 4];
            int size = opcode & 0x000F;
            registers[15] = 0;
            for (int h = 0;h < size;h++) {
                byte b = memory[idx_reg + h];
                for (byte w = 0;w < 8;w++) {
                    if ((b & (1 << (7 - w))) != 0) {
                        int mod_x = x + w;
                        int mod_y = y + h;
                        int width = gfx.GetLength(0);
                        int height = gfx.GetLength(1);

                        while (mod_x < 0) mod_x += width;
                        while (mod_y < 0) mod_y += height;

                        while (mod_x >= width) mod_x -= width;
                        while (mod_y >= height) mod_y -= height;

                        registers[15] = gfx[mod_x, mod_y] ? (byte)1 : registers[15];
                        gfx[mod_x, mod_y] = !gfx[mod_x, mod_y];
                    }
                }
            }
            Debug.WriteLine("Drew sprite at " + x + ", " + y + " with a height of " + size + ".");
        }
        void MiscOpcodes() {
            int reg = (opcode & 0x0F00) >> 8;
            switch (opcode & 0x00FF)
            {
                case 0x07:
                    registers[reg] = delay_timer;
                    Debug.WriteLine("Set V" + reg + " to " + delay_timer + ".");
                    break;
                case 0x0A:
                    wait_reg = reg;
                    wait_flag = true;
                    break;
                case 0x15:
                    delay_timer = registers[reg];
                    Debug.WriteLine("Set delay timer to " + registers[reg] + ".");
                    break;
                case 0x18:
                    sound_timer = registers[reg];
                    Debug.WriteLine("Set sound timer to " + registers[reg] + ".");
                    break;
                case 0x1E:
                    idx_reg += registers[reg];
                    Debug.WriteLine("Added " + registers[reg] + " to index register.");
                    break;
                case 0x29:
                    idx_reg = ((ushort)(registers[reg] * 5));
                    idx_reg += font_ptr;
                    break;
                case 0x33:
                    byte b = registers[reg];
                    Debug.WriteLine("Writing BCD of " + b + " into 0x" + idx_reg.ToString("X") + ".");
                    for (int i = 2;i >= 0;i--) {
                        memory[idx_reg + i] = (byte)(b % 10);
                        Debug.WriteLine("Wrote " + memory[idx_reg + i] + " to 0x" + (idx_reg + i).ToString("X") + ".");
                        b /= 10;
                    }
                    break;
                case 0x55:
                    Debug.WriteLine("Writing V0 to V" + reg + " to 0x" + idx_reg.ToString("X") + ".");
                    for (int i = 0; i <= reg; i++) {
                        memory[idx_reg + i] = registers[i];
                        Debug.WriteLine("Wrote " + registers[i] + " to 0x" + (idx_reg + i).ToString("X") + ".");
                    }
                    if (!compat_settings[(int)CompatibilitySettings.LOAD_STORE])
                    {
                        idx_reg += (ushort)reg;
                    }
                    
                    break;
                case 0x65:
                    Debug.WriteLine("Writing 0x" + idx_reg.ToString("X") + " to 0x" + (idx_reg + reg).ToString("X") + "to V0 to V" + reg + ".");
                    for (int i = 0; i <= reg; i++) {
                        registers[i] = memory[idx_reg + i];
                        Debug.WriteLine("Wrote " + memory[idx_reg + i] + " to V" + i + ".");
                    }
                    if (!compat_settings[(int)CompatibilitySettings.LOAD_STORE])
                    {
                        idx_reg += (ushort)reg;
                    }
                    //Debug.WriteLine("Copied V0 to V" + reg + " to 0x" + idx_reg.ToString("X") + ".");
                    break;
            }
        }
        public bool[,] GetScreen() {
            return gfx;
        }
    }
}
