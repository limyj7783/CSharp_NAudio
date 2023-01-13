//#define FILE_WRITE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using FFmpeg.AutoGen;
using NAudio.Wave;
using System.Runtime.InteropServices;

namespace NaudioTest
{
    public unsafe class FFMpegAACEncoder
    {
        //FFMpeg
        AVCodec* codec;
        AVCodecContext* codec_context;
        AVFrame* frame;
        AVPacket* pkt;
        int data_size;

        Queue<byte> audio_pcm_datas;

        byte[] aac_adts_header;

        //test file write
        WaveFileWriter wave_file;
#if FILE_WRITE
        FileStream fs;
        BinaryWriter bw;
#endif

        //ffmpeg file write


        public FFMpegAACEncoder()
        {
            codec = null;
            codec_context = null;
            frame = null;
            pkt = null;
            data_size = 0;
            audio_pcm_datas = new Queue<byte>();
            aac_adts_header = new byte[7];

            wave_file = new WaveFileWriter(@"C:\Project\CSharp_NAudio\Test0002.wav", new WaveFormat(44100, 16, 1));

#if FILE_WRITE
            fs = new FileStream(@"C:\Project\CSharp_NAudio\Test0002.aac", FileMode.Create, FileAccess.Write);
            bw = new BinaryWriter(fs);
#endif
        }

        public int InitEncoder()
        {
            codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_AAC);
            if (codec == null)
            {
                Console.WriteLine("Error. avcodec_fine_encoder function call fail. codec is null");
                return -1;
            }

            codec_context = ffmpeg.avcodec_alloc_context3(codec);
            if (codec_context == null)
            {
                Console.WriteLine("Error. avcodec_alloc_context3 function call fail. codec_context is null");
                return -2;
            }


            codec_context->bit_rate = 64000;
            codec_context->sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_FLTP;
            if (CheckSampleFmt(codec, codec_context->sample_fmt) == 0)
            {
                Console.WriteLine("Error. SampleFormat Unknown");
                return -3;
            }

            /* select other audio parameters supported by the encoder */
            codec_context->sample_rate = SelectSampleRate(codec);
            codec_context->channel_layout = SelectChannelLayout(codec);
            codec_context->channels = ffmpeg.av_get_channel_layout_nb_channels(codec_context->channel_layout);

            int ret = 0;
            /* open it */
            ret = ffmpeg.avcodec_open2(codec_context, codec, null);
            if (ret < 0)
            {
                Console.WriteLine("Error. avcodec_open2 fail. error code["+ret+"]");
                return -4;
            }

            /* packet for holding encoded output */
            pkt = ffmpeg.av_packet_alloc();
            if (pkt == null)
            {
                Console.WriteLine("Error. av_packet_alloc function call fail. pkt is null");
                return -5;
            }

            /* frame containing input raw audio */
            frame = ffmpeg.av_frame_alloc();
            if (frame == null)
            {
                Console.WriteLine("Error. av_frame_alloc function call fail. frame is null");
                return -6;
            }

            frame->nb_samples = codec_context->frame_size;
            frame->format = (int)codec_context->sample_fmt;
            frame->channel_layout = codec_context->channel_layout;

            ret = ffmpeg.av_frame_get_buffer(frame, 0);
            if (ret < 0)
            {
                Console.WriteLine("Error. av_frame_get_buffer function fall. error code[" + ret + "]");
                return -7;
            }

            data_size = ffmpeg.av_samples_get_buffer_size(null, codec_context->channels, frame->nb_samples, codec_context->sample_fmt, 0);

            return 0;
        }

        /* check that a given sample format is supported by the encoder */
        private unsafe int CheckSampleFmt(AVCodec* codec, AVSampleFormat sample_fmt)
        {
            AVSampleFormat* p = codec->sample_fmts;

            while (*p != AVSampleFormat.AV_SAMPLE_FMT_NONE)
            {
                if (*p == sample_fmt)
                {
                    return 1;
                }

                p++;
            }

            return 0;
        }

        /* just pick the highest supported samplerate */
        private unsafe int SelectSampleRate(AVCodec* codec)
        {
            int* p;
            int best_samplerate = 0;

            if (codec->supported_samplerates == null)
            {
                return 44100;
            }

            p = codec->supported_samplerates;
            while (*p != 0)
            {
                if (best_samplerate == 0 || Math.Abs(44100 - *p) < Math.Abs(44100 - best_samplerate))
                {
                    best_samplerate = *p;
                }

                p++;
            }

            return best_samplerate;
        }

        /* select layout with the highest channel count */
        private unsafe ulong SelectChannelLayout(AVCodec* codec)
        {
            ulong* p;
            ulong best_ch_layout = 0;
            int best_nb_channels = 0;

            if (codec->channel_layouts == null)
            {
                return ffmpeg.AV_CH_LAYOUT_STEREO;
            }

            p = codec->channel_layouts;
            while (*p != 0)
            {
                int nb_channels = ffmpeg.av_get_channel_layout_nb_channels(*p);

                if (nb_channels > best_nb_channels)
                {
                    best_ch_layout = *p;
                    best_nb_channels = nb_channels;
                }

                p++;
            }

            return best_ch_layout;
        }

        public unsafe void UninitEncoder()
        {
            if (frame != null)
            {
                fixed (AVFrame** _frame = &frame)
                {
                    ffmpeg.av_frame_free(_frame);
                }
                frame = null;
            }

            if (pkt != null)
            {
                fixed (AVPacket** _pkt = &pkt)
                {
                    ffmpeg.av_packet_free(_pkt);
                }
                pkt = null;
            }

            if (codec_context != null)
            {
                fixed (AVCodecContext** _codec_context = &codec_context)
                {
                    ffmpeg.avcodec_free_context(_codec_context);
                }

                codec_context = null;
            }
#if FILE_WRITE
            FileClose();
#endif
        }

        public unsafe int PushData(byte[] data, int length)
        {
            for (int i = 0; i < length; i++)
                audio_pcm_datas.Enqueue(data[i]);
            encode();
            return 0;
        }

        //private bool encode(AVCodecContext* ctx, AVFrame* frame, AVPacket* pkt)
        private bool encode()
        {
            byte[] pcm_data = new byte[data_size];
            for(int i=0; i<data_size; i++)
                pcm_data[i] = audio_pcm_datas.Dequeue();

            wave_file.Write(pcm_data, 0, data_size);
            wave_file.Flush();

            int ret = 0;

            ret = ffmpeg.av_frame_make_writable(frame);
            if (ret < 0)
            {
                Console.WriteLine("Error. av_frame_make_writable error code[" + ret + "]");
                return false;
            }


            fixed(byte* ptr = pcm_data)
            {
                frame->data[0] = ptr;
                ret = ffmpeg.avcodec_send_frame(codec_context, frame);
            }
            if (ret < 0)
            {
                Console.WriteLine("avcodec_send_frame fail [" + ret + "]");
                return false;
            }

            while(ret >= 0)
            {
                ret = ffmpeg.avcodec_receive_packet(codec_context, pkt);
                if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF)
                    return true;
                else if (ret < 0)
                {
                    return false;
                }

                GetAACHeader(pkt);

                byte[] data = new byte[pkt->size];
                for (int i = 0; i < pkt->size; i++)
                    data[i] = pkt->data[i];

#if FILE_WRITE
                WriteFile(data);
#endif
                ffmpeg.av_packet_unref(pkt);
            }

            return true;
        }

#if FILE_WRITE
        private void WriteFile(byte[] data)
        {
            
            bw.Write(aac_adts_header);
            bw.Write(data);

            string file_name = @"C:\Project\CSharp_NAudio\test.aac";
            using (FileStream fs = File.Open(file_name, FileMode.OpenOrCreate))
            {

                fs.Seek(0, SeekOrigin.End);
                fs.Write(aac_adts_header, 0, aac_adts_header.Length);
                fs.Seek(0, SeekOrigin.End);
                fs.Write(data, 0, data.Length);
            }
        }
#endif

#if FILE_WRITE
        public void FileClose()
        {
            bw.Close();
            fs.Close();
        }
#endif

        private int GetAACHeader(AVPacket* pkt)
        {
            int profile = 2;        //AAC LC
            int freqIdx = 11;       //8000Hz
            int chanCfg = 1;        //MPEG-4 Audio Channel Configuration. 1 Channel front-center

            aac_adts_header[0] = (byte)0xFF;      // 11111111     = syncword  
            aac_adts_header[1] = (byte)0xF9;      // 1111 1 00 1  = syncword MPEG-2 Layer CRC  
            aac_adts_header[2] = (byte)(((profile - 1) << 6) + (freqIdx << 2) + (chanCfg >> 2));
            aac_adts_header[3] = (byte)(((chanCfg & 3) << 6) + ((7 + pkt->size) >> 11));
            aac_adts_header[4] = (byte)(((7 + pkt->size) & 0x7FF) >> 3);
            aac_adts_header[5] = (byte)((((7 + pkt->size) & 7) << 5) + 0x1F);
            aac_adts_header[6] = (byte)0xFC;
            return 0;
        }

    }
}
