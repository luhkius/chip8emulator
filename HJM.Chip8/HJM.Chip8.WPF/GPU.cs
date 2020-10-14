using System;
using System.Diagnostics;
using System.Threading;
using Serilog;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Windows.Input;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using System.Text;

namespace HJM.Chip8.WPF
{
    public class GPU
    {
        private const int ClockSpeed = 500;

        private DirectBitmap _bmp;
        private MemoryStream _bmpStream = new MemoryStream();
        WriteableBitmap _wbmp;
        MemoryStream _audioStream;
        System.Media.SoundPlayer _soundPlayer;
        private int _audioTime = 1;
        private int _minAudioTime = ClockSpeed/20;

        private readonly Thread _emulatorThread;
        private readonly CPU.Chip8 _chip8;
        private readonly System.Windows.Controls.Image _renderImage;

        private bool _threadStopped = false;
        private uint[] _displayBuffer = new uint[4096];
        private uint _bufferedFrameMask = 0b_1111_1111_1111_1110;

        public GPU(System.Windows.Controls.Image image)
        {
            _renderImage = image;

            _emulatorThread = new Thread(RunCycles);
            _chip8 = new CPU.Chip8();
        }

        public void Initialize()
        {           
            _chip8.Initalize();
            //_chip8.LoadGame(@"C:\Users\Hayden\Downloads\myChip8-bin-src\myChip8-bin-src\PONG2.c8");
            _chip8.LoadGame(@"C:\Users\Luke\Downloads\c8games\PONG2.ch8");
            //_chip8.LoadGame(@"C:\Users\Luke\Downloads\c8games\BRIX.ch8");

            _audioStream = GenerateAudio();
            _soundPlayer = new System.Media.SoundPlayer(_audioStream);
            _soundPlayer.Load();

            _bmp = new DirectBitmap(64, 32);
            _wbmp = new WriteableBitmap(64, 32, 72, 72, System.Windows.Media.PixelFormats.Indexed8, BitmapPalettes.Halftone256);

            _renderImage.Dispatcher.Invoke(new Action(() => {
                
                _renderImage.Source = _wbmp;
            }));

            _emulatorThread.Start();
            Log.Information("Emulator thread started.");
        }

        public void Quit()
        {
            if (_bmp != null)
                _bmp.Dispose();

            if (_bmpStream != null)
                _bmpStream.Dispose();

            _audioStream.Dispose();
            _soundPlayer.Dispose();

            _threadStopped = true;
            Log.Information("Emulator thread stopped.");

            Environment.Exit(0);
        }
        
        protected void CheckInput()
        {
            List<System.Windows.Input.Key> keys = MainWindow._pressedKeys;

            if (keys.Contains(Key.Escape))
                Quit();

            // set all the keys
            _chip8.Key[0] = Convert.ToByte(keys.Contains(Key.X));
            _chip8.Key[1] = Convert.ToByte(keys.Contains(Key.D1));
            _chip8.Key[2] = Convert.ToByte(keys.Contains(Key.D2));
            _chip8.Key[3] = Convert.ToByte(keys.Contains(Key.D3));
            _chip8.Key[4] = Convert.ToByte(keys.Contains(Key.Q));
            _chip8.Key[5] = Convert.ToByte(keys.Contains(Key.W));
            _chip8.Key[6] = Convert.ToByte(keys.Contains(Key.E));
            _chip8.Key[7] = Convert.ToByte(keys.Contains(Key.A));
            _chip8.Key[8] = Convert.ToByte(keys.Contains(Key.S));
            _chip8.Key[9] = Convert.ToByte(keys.Contains(Key.D));
            _chip8.Key[0xA] = Convert.ToByte(keys.Contains(Key.Z));
            _chip8.Key[0xB] = Convert.ToByte(keys.Contains(Key.C));
            _chip8.Key[0xC] = Convert.ToByte(keys.Contains(Key.D4));
            _chip8.Key[0xD] = Convert.ToByte(keys.Contains(Key.R));
            _chip8.Key[0xE] = Convert.ToByte(keys.Contains(Key.F));
            _chip8.Key[0xF] = Convert.ToByte(keys.Contains(Key.V));
        }

        private void Draw()
        {
            // LoadContent has been called at this point
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    int index = (y * 64) + x;
                    if (_chip8.Graphics[index] == 1 || _displayBuffer[index] > 0)
                    {
                        _bmp.SetPixel(x, y, Color.White);
                        _displayBuffer[index] |= (uint)((_chip8.Graphics[index] << 15) & 0x8000);   //If _chip8.Graphics[index] == 1, set the first bit in displayBuffer[index] to 1
                    }
                    else
                        _bmp.SetPixel(x, y, Color.Black);

                    _displayBuffer[index] = (uint)((_displayBuffer[index] >> 1) & _bufferedFrameMask);  //Shift buffered frames right, apply mask to cap amount of buffered frames
                }
            }

            _renderImage.Dispatcher.Invoke(new Action(() => {
                _wbmp.WritePixels(new System.Windows.Int32Rect(0, 0, 64, 32), _bmp.Bits, _bmp.Width, 0);
                //_wbmp.Lock();
                //Marshal.Copy(_bmp.Bits, 0, _wbmp.BackBuffer, _bmp.Bits.Length);
                //_wbmp.AddDirtyRect(new System.Windows.Int32Rect(0, 0, _bmp.Width, _bmp.Height));
                //_wbmp.Unlock();
            }));

            if (_chip8.SoundFlag)
            {
                if(_audioTime != 0)
                    _soundPlayer.PlayLooping();
                _audioTime = 0;
            }
            else if (_audioTime < _minAudioTime)
                _audioTime++;
            else
                _soundPlayer.Stop();
        }

        protected void RunCycles()
        {
            int millesecondsPerCycle = 1000 / ClockSpeed;

            Stopwatch s = new Stopwatch();
            s.Start();

            while (true)
            {
                CheckInput();

                if (_threadStopped)
                    break;

                _chip8.EmulateCycle();

                Draw();

                while (s.ElapsedMilliseconds < millesecondsPerCycle)
                    Thread.Sleep(1);

                s.Restart();
            }
        }

        public class DirectBitmap : IDisposable
        {
            public Bitmap Bitmap { get; private set; }
            public byte[] Bits { get; private set; }
            public bool Disposed { get; private set; }
            public int Height { get; private set; }
            public int Width { get; private set; }

            public GCHandle BitsHandle { get; private set; }
            public IntPtr PtrHandle { get; private set; }
            public IntPtr prealloc = (IntPtr)0x0;

            public DirectBitmap(int width, int height)
            {
                Width = width;
                Height = height;
                Bits = new byte[width * height];
                BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
                Bitmap = new Bitmap(width, height, width, PixelFormat.Format8bppIndexed, BitsHandle.AddrOfPinnedObject());
                PtrHandle = GCHandle.ToIntPtr(BitsHandle);
            }

            public void SetPixel(int x, int y, Color colour)
            {
                //int index = x + (y * Width);
                //int col = colour.ToArgb();

                //Bits[index] = col;
                Bits[x + (y * Width)] = colour.R;
            }

            public Color GetPixel(int x, int y)
            {
                int index = x + (y * Width);
                int col = Bits[index];
                Color result = Color.FromArgb(col);

                return result;
            }


            public void Dispose()
            {
                if (Disposed) return;
                Disposed = true;
                Bitmap.Dispose();
                BitsHandle.Free();
            }
        }

        public static MemoryStream GenerateAudio()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            int samplesPerSecond = 8000;    //8kbit/s
            short bitsPerSample = 16;
            short frameSize = (short)((bitsPerSample + 7) / 8);
            int bytesPerSecond = samplesPerSecond * frameSize;
            int samples = 72; //One second of audio at 8Kbit/s
            int dataChunkSize = samples * frameSize;
            int fileSize = 36 + dataChunkSize;
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));  //RIFF header
            writer.Write(fileSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));   //WAVE header
            writer.Write(Encoding.ASCII.GetBytes("fmt "));  //fmt
            writer.Write(16);   //Format Chunk Size
            writer.Write((short)1); //Format Type
            writer.Write((short)1); //Tracks
            writer.Write(samplesPerSecond);
            writer.Write(bytesPerSecond);
            writer.Write(frameSize);
            writer.Write(bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataChunkSize);
            for (double i = 0; i < samples; i++)
            {
                //writer.Write((short)i * 400);
                //writer.Write((short));
                double t = (i / samplesPerSecond) * 2000 * Math.PI;
                writer.Write((short)(Math.Sin(t) * 10000));
            }
            for (int i = samples/2; i > 0; i--)
            {
                //writer.Write((short)500 * 50);
                //writer.Write((short)i * 1000);
            }

            writer.Flush();
            ms.Position = 0;
            return ms;
        }

    }
}
