using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SFML.Audio;
using SFML.Window;
using SFML.Graphics;
using System.IO;
using System.Diagnostics;
using sys_draw = System.Drawing;

namespace CHIP.NET
{
    class Program
    {
        static Dictionary<Keyboard.Key,byte> keymap;
        static ushort keyInput;
        static bool done;
        static Emulator emulator;
        static Sound beep = new Sound(new SoundBuffer("./beep.wav"));
        static void Main(string[] args)
        {
            if (args.Length == 0) {
                Console.Write(
                    "Usage: CHIP.NET PROGRAM_PATH\n" +
                    "Note: Some ROMs require compatibilty flags. Compatibility flags:\n" + 
                    " --load_store\n" + 
                    " --shift\n"
                );
                return;
            }
            //tone.RepeatSpeed = 5;
            RenderWindow window = new RenderWindow(new VideoMode(64 * 10, 32 * 10), "CHIP.NET");
            
            
            Texture icon = new Texture("./icon.png");
            window.SetIcon(icon.Size.X, icon.Size.Y, icon.CopyToImage().Pixels);
            
            //window.SetIcon(64, 64, File.ReadAllBytes("./icon.png"));
            window.Closed += new EventHandler(OnClose);
            window.KeyPressed += new EventHandler<KeyEventArgs>(OnKeyPressed);
            window.KeyReleased += new EventHandler<KeyEventArgs>(OnKeyReleased);

            keymap = new Dictionary<Keyboard.Key, byte>();

            keymap.Add(Keyboard.Key.Num1, 0x1);
            keymap.Add(Keyboard.Key.Num2, 0x2);
            keymap.Add(Keyboard.Key.Num3, 0x3);
            keymap.Add(Keyboard.Key.Num4, 0xC);

            keymap.Add(Keyboard.Key.Q, 0x4);
            keymap.Add(Keyboard.Key.W, 0x5);
            keymap.Add(Keyboard.Key.E, 0x6);
            keymap.Add(Keyboard.Key.R, 0xD);

            keymap.Add(Keyboard.Key.A, 0x7);
            keymap.Add(Keyboard.Key.S, 0x8);
            keymap.Add(Keyboard.Key.D, 0x9);
            keymap.Add(Keyboard.Key.F, 0xE);

            keymap.Add(Keyboard.Key.Z, 0xA);
            keymap.Add(Keyboard.Key.X, 0x0);
            keymap.Add(Keyboard.Key.C, 0xB);
            keymap.Add(Keyboard.Key.V, 0xF);

            emulator = new Emulator(File.ReadAllBytes(args[0]), File.ReadAllBytes("./font.bin"));
            for (int i = 1;i < args.Length;i++) {
                CompatibilitySettings compat = CompatibilitySettings.LOAD_STORE;
                bool unknown = false;
                switch (args[i]) {
                    case "--load_store":
                        compat = CompatibilitySettings.LOAD_STORE;
                        break;
                    case "--shift":
                        compat = CompatibilitySettings.SHIFT;
                        break;
                    default:
                        unknown = true;
                        break;
                }
                if (unknown) {
                    Console.WriteLine("Unknown flag " + args[i]);
                } else {
                    emulator.EnableCompat(compat);
                    Console.WriteLine("Enabled " + args[i]);
                }
            }
            Color OffColor = new Color(143, 145, 133);
            Color OnColor = new Color(17, 29, 43);

            window.SetActive();
            //window.SetFramerateLimit(1);
            Stopwatch deltaTimer = new Stopwatch();
            deltaTimer.Start();
            //beep.Play();
            RectangleShape pixel = new RectangleShape();
            pixel.FillColor = OnColor;
            while (window.IsOpen) {
                window.Clear(OffColor);
                window.DispatchEvents();
                if (!done && deltaTimer.ElapsedMilliseconds > 1000 / 500)
                {
                    deltaTimer.Stop();
                    done = emulator.Step(keyInput, (int)deltaTimer.ElapsedMilliseconds);
                    if (emulator.beepFlag) {
                        emulator.beepFlag = false;
                        beep.Play();
                    }
                    deltaTimer.Restart();
                }

                bool[,] gfx = emulator.GetScreen();
                //gfx[0, 31] = true;
                int gfxWidth = gfx.GetLength(0);
                int gfxHeight = gfx.GetLength(1);

                pixel.Size = new SFML.System.Vector2f(window.DefaultView.Size.X / (float)gfxWidth, window.DefaultView.Size.Y / (float)gfxHeight);
                
                for (int x = 0;x < gfxWidth;x++) {
                    for (int y = 0; y < gfxHeight; y++){
                        if (gfx[x, y]) {
                            pixel.Position = new SFML.System.Vector2f(x * pixel.Size.X, y * pixel.Size.Y);
                            window.Draw(pixel);
                        }
                    }
                }

                window.Display();
            }
        }
        static void OnClose(object sender, EventArgs e)
        {
            RenderWindow window = (RenderWindow)sender;
            window.Close();
        }
        static void OnKeyPressed(object sender, KeyEventArgs e)
        {
            KeyHandler(e.Code, true);
        }
        static void OnKeyReleased(object sender, KeyEventArgs e)
        {
            KeyHandler(e.Code, false);
        }
        static void KeyHandler(Keyboard.Key k, bool set) {
            byte b;
            if (keymap.TryGetValue(k, out b))
            {
                keyInput = (ushort)(set ? (keyInput | (1 << b)) : (keyInput & (0 << b)));
            }
        }
    }
}
