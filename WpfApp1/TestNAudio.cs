#define NAUDIO_WOUT_DIRECTSOUND

#define TEST_DEV_WASAPI



using log4net;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace WpfApp1
{

    /// <summary>
    /// wave in, out device 
    /// </summary>
    public class WaveDevice
    {
        public WaveDevice(int id, string name)
        {
            this.DeviceID = id;
            this.DeviceName = name;
        }
        public int DeviceID { get; set; }
        public string DeviceName { get; set; }
        public override string ToString()
        {
            return this.DeviceName;
        }
    }

    public class TestNAudio
    {


        /// <summary>
        /// waveIn (MIC)
        /// </summary>
        private IWaveIn m_capture;
        private List<WaveDevice> m_waveInDevices = new List<WaveDevice>();
        private int m_selectedWaveInIndex = 0;

        /// <summary>
        /// waveOut (speaker)
        /// </summary>
        private IWavePlayer m_waveOut;
        private List<WaveDevice> m_waveOutDevices = new List<WaveDevice>();
        private int m_selectedWaveOutIndex = 0;
        private BufferedWaveProvider m_bufferedWaveProvider;

        /// <summary>
        /// MMDevice (in, out)
        /// volume 제어를 위해 사용됨
        /// </summary>
        private IEnumerable<MMDevice> m_captureDevices;
        private IEnumerable<MMDevice> m_renderDevices;



        private void openWaveIn()
        {
            try
            {
                // WASAPI(Windows Audio Session API) - Windows Vista/7
                // WASAPI는 Vista SP1 이후부터 사용할 수 있는 인터페이스로 KMixer가 없어지면서 생겼다. 
                // WASAPI는 두 가지 방식이 있는데, 하나는 여러 오디오 스트림이 섞이고 효과가 적용된 상태로 사운드 장치로 전송되는 공유 방식(Shared Mode)이고 다른 하나는 미디어 재생기의 오디오 스트림이 직접 사운드 장치로 전송되는 단독 방식(Exclusive Mode)이다.
                // WASAPI는 downsampling할 수 없다 (http://stackoverflow.com/questions/14861468/wasapicapture-naudio)
                //m_capture = new WasapiCapture(SelectedCaptureDevice);
                //m_capture.ShareMode = AudioClientShareMode.Shared;

                // Allows recording using the Windows waveIn APIs
                m_capture = new WaveIn();
                m_capture.WaveFormat = new WaveFormat(16000, 16, 1);     // wave: 44100, 16, 2,  PCM: 8000, 16, 1
                m_capture.DataAvailable += m_capture_DataAvailable;
                ((WaveIn)m_capture).DeviceNumber = m_selectedWaveInIndex;
                ((WaveIn)m_capture).BufferMilliseconds = 10;            // 10ms 단위로 전송
                ((WaveIn)m_capture).NumberOfBuffers = 3;

                m_capture.StartRecording();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private int cnt = 0;
        private void m_capture_DataAvailable(object sender, WaveInEventArgs e)
        {
            int avail_size = m_bufferedWaveProvider.BufferLength - m_bufferedWaveProvider.BufferedBytes;
            logger.Info($"{cnt++}: waveout avail: {avail_size} bytes");
            m_bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        /// <summary>
        /// 오디오 입력장치를 닫습니다
        /// </summary>
        private void closeWaveIn()
        {
            // close waveIn
            if (m_capture != null)
            {
                try
                {
                    m_capture.StopRecording();
                    m_capture.Dispose();
                    m_capture = null;
                }
                catch (Exception e)
                {
                    throw e;
                }
            }
        }



        WaveIn src = null;
        DirectSoundOut wave_out = null;
        BufferedWaveProvider wave_provider = null;
        void open_naudio()
        {
            int dev_idx = 0;
            src = new WaveIn();
            src.DeviceNumber = dev_idx;
            src.WaveFormat = new WaveFormat(16000, 1);
            src.DataAvailable += Src_DataAvailable;
            ((WaveIn)src).BufferMilliseconds = 10;            // 10ms 단위로 전송
            ((WaveIn)src).NumberOfBuffers = 2; //



            wave_provider = new BufferedWaveProvider(src.WaveFormat);
            wave_out = new DirectSoundOut();
            wave_out.Init(wave_provider);

            src.StartRecording();
            wave_out.Play();
        }

#if TEST_DEV_WASAPI
        IWaveIn newWaveIn;

        IWavePlayer wavePlayer;
        BufferedWaveProvider write_pcm;

        void open_naudio_WASAPI()
        {
            var enumerator = new MMDeviceEnumerator();
            var endPoints = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            foreach (var endPoint in endPoints)
            {
                Debug.WriteLine(string.Format("{0} ({1})", endPoint.FriendlyName, endPoint.DeviceFriendlyName));
            }
            var dev = endPoints[0];
            var wascap = new WasapiCapture(dev);
            
            
            newWaveIn = wascap;
            newWaveIn.WaveFormat = new WaveFormat(16000, 1);
            newWaveIn.DataAvailable += Src_DataAvailable;

            newWaveIn.StartRecording();



            BufferedWaveProvider reader = new BufferedWaveProvider(new WaveFormat(16000, 16, 1));
            //reader.BufferLength = reader.WaveFormat.AverageBytesPerSecond / 2;
            write_pcm = reader;

            var sampleChannel = new SampleChannel(reader, true);
            //sampleChannel.PreVolumeMeter += OnPreVolumeMeter;
            //setVolumeDelegate = vol => sampleChannel.Volume = vol;
            var wave_provider = new MeteringSampleProvider(sampleChannel);
            //postVolumeMeter.StreamVolume += OnPostVolumeMeter;


            // device
            enumerator = new MMDeviceEnumerator();
            endPoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var endPoint in endPoints)
            {
                Debug.WriteLine(string.Format("{0} ({1})", endPoint.FriendlyName, endPoint.DeviceFriendlyName));
            }
            dev = endPoints[2];
            wavePlayer = new WasapiOut(
                dev,
                AudioClientShareMode.Shared,
                false,
                0);

            wavePlayer.Init(wave_provider);
            wavePlayer.Play();

        }
#endif


        private void Src_DataAvailable(object sender, WaveInEventArgs e)
        {
#if TEST_DEV_WASAPI
            int avail_size = write_pcm.BufferLength - write_pcm.BufferedBytes;
            logger.Info($"{cnt++}: waveout avail: {avail_size}, {write_pcm.BufferLength}, {write_pcm.BufferedBytes}, " +
                $"Rx:{e.BytesRecorded}");

            if (write_pcm != null)
                write_pcm.AddSamples(e.Buffer, 0, e.BytesRecorded);
#else
            int avail_size = wave_provider.BufferLength - wave_provider.BufferedBytes;
            logger.Info($"{cnt++}: waveout avail: {avail_size} bytes, Rx:{e.BytesRecorded}");

            wave_provider.AddSamples(e.Buffer, 0, e.BytesRecorded);
#endif
        }

        void close_naudio()
        {
#if TEST_DEV_WASAPI
            if (wavePlayer != null)
            {
                wavePlayer.Stop();
                wavePlayer.Dispose();
                wavePlayer = null;
            }
            if (newWaveIn != null)
            {
                newWaveIn.StopRecording();
                newWaveIn.Dispose();
                newWaveIn = null;
            }
#else
            if (wave_out != null)
            {
                wave_out.Stop();
                wave_out.Dispose();
                wave_out = null;
            }
            if (src != null)
            {
                src.StopRecording();
                src.Dispose();
                src = null;
            }
#endif
        }



        public void Open(IntPtr hwnd)
        {
#if TEST_DEV_WASAPI
            open_naudio_WASAPI();
#else
            open_naudio();
            //openWaveOut(hwnd);
            //openWaveIn();
#endif
        }

        public void Close()
        {
            cnt = 0;
            close_naudio();
            //closeWaveIn();
            //closeWaveOut();
        }


        public void Flush()
        {
#if TEST_DEV_WASAPI
            newWaveIn.StopRecording();
            Thread.Sleep(100);
            newWaveIn.StartRecording();
#endif
        }




        private static readonly log4net.ILog logger =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        /// <summary>
        /// singletone
        /// </summary>
        private static TestNAudio m_instance;
        public static TestNAudio getInstance()
        {
            if (m_instance == null)
            {
                m_instance = new TestNAudio();
                //((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level =
                //    log4net.Core.Level.Info;
                log4net.Config.XmlConfigurator.Configure();

            }
            return m_instance;
        }

        
    }

}
