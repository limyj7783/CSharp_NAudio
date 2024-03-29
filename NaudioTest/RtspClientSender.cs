﻿//#define RTSP_FILE_DUMP

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FFmpeg.AutoGen;


namespace ViewLiveClientMain._2nd_Dev
{
    unsafe class RtspClientSender
    {
        RtspClientWrapper streamer;
        EN_SUPPORTABLE_CODEC_TYPE audio_codec;

        bool is_init;
        int input_stream_index;

#if RTSP_FILE_DUMP
        string file_path;
        BinaryWriter aac_file;
#endif

        public RtspClientSender(string rtsp_url)
        {
#if RTSP_FILE_DUMP
            file_path = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.Parent.FullName;
            aac_file = new BinaryWriter(new FileStream(file_path + @"\rtsp_input.aac", FileMode.Create, FileAccess.Write));
#endif
            Console.WriteLine("Info. Rtsp client sender [" + rtsp_url + "]");
            is_init = false;
            input_stream_index = 0;
            //string sunapi_uri = @"/profile1/media.smp";
            //string sunapi_uri = @"/profile1/media";
            //string url = @"rtsp://" + ip + ":" + port.ToString() + sunapi_uri;
            audio_codec = EN_SUPPORTABLE_CODEC_TYPE.EN_SUPPORTABLE_CODEC_TYPE_UNKNOWN;
            streamer = new RtspClientWrapper(rtsp_url, EN_LOG_LEVEL_TYPE.EN_LOG_LEVEL_TYPE_INFO);
        }

        public int Initialize(AVMediaType avmedia_type, AVCodecID avcodec, int sample_rate, int channel, int bits_aligned, byte* extra_data, int extra_data_size)
        {
            Console.WriteLine("Info. Rtsp client sender Inialize.");
            if (avmedia_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
            {
                if (avcodec == AVCodecID.AV_CODEC_ID_AAC)
                {
                    audio_codec = EN_SUPPORTABLE_CODEC_TYPE.EN_SUPPORTABLE_CODEC_TYPE_AAC;

                    if (streamer.AddAudioStream(input_stream_index, audio_codec, sample_rate, channel, bits_aligned, extra_data, extra_data_size) == 0)
                    {
                        Console.WriteLine("Info. Success to add a audio stream infomation.");
                    }
                    else
                    {
                        Console.Error.WriteLine("Error. failed to add a audio stream information");
                        return -1;
                    }
                }
                else if (avcodec == AVCodecID.AV_CODEC_ID_PCM_MULAW)
                {
                    audio_codec = EN_SUPPORTABLE_CODEC_TYPE.EN_SUPPORTABLE_CODEC_TYPE_PCM;

                    if (streamer.AddAudioStream(input_stream_index, audio_codec, sample_rate, channel, bits_aligned, extra_data, extra_data_size) == 0)
                    {
                        Console.WriteLine("Info. Success to add a audio stream infomation.");
                    }
                    else
                    {
                        Console.Error.WriteLine("Error. failed to add a audio stream information");
                        return -1;
                    }
                }
                else
                {
                    Console.Error.WriteLine("Error. Unknown audio codec.");
                    return -1;
                }

            }
            else
            {
                Console.Error.WriteLine("Error. Unknown media type.");
                return -1;
            }

            if (StartStream() < 0)
            {
                Console.WriteLine("failed to start stream");
                return -1;
            }

            Console.WriteLine("Info. Rtsp client sender Inialize OK.");
            is_init = true;
            return 0;
        }

        public void Uninitalize()
        {
            Console.WriteLine("Info. Rtsp client sender Uninitalize.");
            StopStream();
            is_init = false;
#if RTSP_FILE_DUMP
            aac_file.Close();
#endif
        }

        public bool IsInitalize()
        {
            return is_init;
        }

        private int StartStream()
        {
            if (streamer.StartStream(EN_RTSP_OPT.EN_RTSP_OPT_TCP_INTERLEAVED, 3000) != 0)
                return -1;
            else
                return 0;
        }


        private void StopStream()
        {
            if (streamer != null)
                streamer.StopStream();
        }

        public int SendStream(byte* data, int data_size, Int64 pts, Int64 dts, int time_scale)
        {
#if RTSP_FILE_DUMP
            for(int i=0; i<data_size; i++)
            {
                aac_file.Write(data[i]);
            }
#endif
            //Console.WriteLine("===========> Debug. pts[" + pts + "] dts[" + dts + "] timescale[" + time_scale + "]");
            if (streamer == null || audio_codec != EN_SUPPORTABLE_CODEC_TYPE.EN_SUPPORTABLE_CODEC_TYPE_PCM || 
                streamer.SendStream(input_stream_index, audio_codec, data, data_size, pts, dts, time_scale) < 0)
            {
                Console.Error.WriteLine("Error. Send Stream failed,");
                StopStream();
                return -1;
            }

            return 0;
        }
    }
}
