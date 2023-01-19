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

namespace ViewLiveClientMain._2nd_Dev
{
    public unsafe class FFMpegAACEncoder
    {
        bool isInit;

        //FFMpeg Encoding
        AVCodec* codec;
        AVCodecContext* codec_context;
        AVFrame* frame;
        AVPacket* pkt;
        int data_size;

        //FFMpeg Resample
        SwrContext* resampleContext;
        byte** sourceData;
        byte** destinationData;

        long sourceChannelLayout;
        long destinationChannelLayout;
        int sourceSampleRate;
        int destinationSampleRate;
        AVSampleFormat sourceSampleFormat;
        AVSampleFormat destinationSampleFormat;
        int sourceSamplesCount;
        int sourceChannelsCount;

        int destinationSampleCount;
        int maxDestinationSampleCount;
        int destinationLinesize;
        int destinationChanelsCount;

        //data buffer
        Queue<byte> audio_raw_datas;
        Queue<byte> audio_resampled_datas;

        //adts header
        byte[] aac_adts_header;
        const int aac_adts_header_length = 7;

#if FILE_WRITE
        //file dump
        BinaryWriter pcm_file;
        BinaryWriter aac_file;
        WaveFileWriter wave_file;
#endif

        //pts
        long pts;
        int time_scale;

        RtspClientSender rtsp_sender;

        public FFMpegAACEncoder(int sample_rate, int bit_aligned, int channels)
        {
#if FILE_WRITE
            string file_path = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.Parent.FullName;
            pcm_file = new BinaryWriter(new FileStream(file_path + @"\resampled.pcm", FileMode.Create, FileAccess.Write));
            aac_file = new BinaryWriter(new FileStream(file_path + @"\output.aac", FileMode.Create, FileAccess.Write));
            wave_file = new WaveFileWriter(file_path + @"\output.wav", new WaveFormat(sample_rate, bit_aligned, channels));
#endif
            Console.WriteLine("Info. AAC Encoder samplerate[" + sample_rate + "], bit[" + bit_aligned + "], channels[" + channels + "]");

            isInit = false;

            codec = null;
            codec_context = null;
            frame = null;
            pkt = null;
            data_size = 0;
            audio_resampled_datas = new Queue<byte>();
            audio_raw_datas = new Queue<byte>();
            aac_adts_header = new byte[aac_adts_header_length];

            resampleContext = null;
            sourceData = null;
            destinationData = null;

            if (channels == 1)
                sourceChannelLayout = (long)ffmpeg.AV_CH_LAYOUT_MONO;
            else
            {
                Console.Error.WriteLine("Error. not support channel. channels[" + channels + "]");
                return;
            }

            destinationChannelLayout = (long)ffmpeg.AV_CH_LAYOUT_MONO;
            sourceSampleRate = sample_rate;
            destinationSampleRate = 44100;

            if(bit_aligned == 32)
            {
                sourceSampleFormat = AVSampleFormat.AV_SAMPLE_FMT_S32;
            }
            else
            {
                Console.Error.WriteLine("Error. not support bit. bit[" + bit_aligned + "]");
                return;
            }

            destinationSampleFormat = AVSampleFormat.AV_SAMPLE_FMT_FLTP;
            sourceSamplesCount = 1024;
            sourceChannelsCount = 0;

            destinationSampleCount = 0;
            maxDestinationSampleCount = 0;
            destinationLinesize = 0;
            destinationChanelsCount = 0;

            pts = 0;
            time_scale = 0;

            if (InitResample() < 0)
            {
                UninitResample();
                Console.Error.WriteLine("Error. Initialize Resampler Failed.");
                return;
            }

            if (InitEncoder() < 0)
            {
                UninitEncoder();
                Console.Error.WriteLine("Error. Initailze AAC Encoder Failed.");
                return;
            }

            rtsp_sender = new RtspClientSender("127.0.0.1", 8554);
            
            if(rtsp_sender.Initialize(AVMediaType.AVMEDIA_TYPE_AUDIO, AVCodecID.AV_CODEC_ID_AAC, codec_context->sample_rate, (int)codec_context->channel_layout, bit_aligned, codec_context->extradata, codec_context->extradata_size) < 0)
            {
                rtsp_sender.Uninitalize();
                return;
            }

            isInit = true;
            Console.WriteLine("Info. Initialize success AAC Encoder.");
        }

        public bool IsInitailze()
        {
            return isInit;
        }

        public void Uninitalize()
        {
#if FILE_WRITE
            if(pcm_file != null)
            {
                pcm_file.Close();
                pcm_file.Dispose();
                pcm_file = null;
            }
            if(aac_file != null)
            {
                aac_file.Close();
                aac_file.Dispose();
                aac_file = null;
            }
            if(wave_file != null)
            {
                wave_file.Close();
                wave_file.Dispose();
                wave_file = null;
            }
#endif
            UninitResample();
            UninitEncoder();

            rtsp_sender.Uninitalize();

            isInit = false;

            Console.WriteLine("Info. Uninitalize AAC Encoder OK.");
        }

        private int InitEncoder()
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
                return -1;
            }


            codec_context->bit_rate = 64000;
            codec_context->sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_FLTP;
            if (CheckSampleFmt(codec, codec_context->sample_fmt) == 0)
            {
                Console.WriteLine("Error. SampleFormat Unknown");
                return -1;
            }

            /* select other audio parameters supported by the encoder */
            codec_context->sample_rate = destinationSampleRate; //SelectSampleRate(codec);
            codec_context->channel_layout = (ulong)destinationChannelLayout; // ffmpeg.AV_CH_LAYOUT_MONO;// SelectChannelLayout(codec);
            codec_context->channels = ffmpeg.av_get_channel_layout_nb_channels(codec_context->channel_layout);

            int ret = 0;
            /* open it */
            ret = ffmpeg.avcodec_open2(codec_context, codec, null);
            if (ret < 0)
            {
                Console.WriteLine("Error. avcodec_open2 fail. error code["+ret+"]");
                return -1;
            }

            /* packet for holding encoded output */
            pkt = ffmpeg.av_packet_alloc();
            if (pkt == null)
            {
                Console.WriteLine("Error. av_packet_alloc function call fail. pkt is null");
                return -1;
            }

            /* frame containing input raw audio */
            frame = ffmpeg.av_frame_alloc();
            if (frame == null)
            {
                Console.WriteLine("Error. av_frame_alloc function call fail. frame is null");
                return -1;
            }

            frame->nb_samples = codec_context->frame_size;
            frame->format = (int)codec_context->sample_fmt;
            frame->channel_layout = codec_context->channel_layout;

            ret = ffmpeg.av_frame_get_buffer(frame, 0);
            if (ret < 0)
            {
                Console.WriteLine("Error. av_frame_get_buffer function fall. error code[" + ret + "]");
                return -1;
            }

            data_size = ffmpeg.av_samples_get_buffer_size(null, codec_context->channels, frame->nb_samples, codec_context->sample_fmt, 0);
            return 0;
        }

        private int InitResample()
        {            
            /* Create resampler context */
            resampleContext = ffmpeg.swr_alloc();
            if (resampleContext == null)
            {
                Console.Error.Write("Error. Could not allocate resampler context\n");
                return -1;
            }

            /* Set options */
            ffmpeg.av_opt_set_int(resampleContext, "in_channel_layout", sourceChannelLayout, 0);
            ffmpeg.av_opt_set_int(resampleContext, "in_sample_rate", sourceSampleRate, 0);
            ffmpeg.av_opt_set_sample_fmt(resampleContext, "in_sample_fmt", sourceSampleFormat, 0);

            ffmpeg.av_opt_set_int(resampleContext, "out_channel_layout", destinationChannelLayout, 0);
            ffmpeg.av_opt_set_int(resampleContext, "out_sample_rate", destinationSampleRate, 0);
            ffmpeg.av_opt_set_sample_fmt(resampleContext, "out_sample_fmt", destinationSampleFormat, 0);

            int ret = 0;
            /* Initialize the resampling context */
            ret = ffmpeg.swr_init(resampleContext);
            if (ret < 0)
            {
                Console.Error.Write("Error. Failed to initialize the resampling context\n");
                return -1;
            }

            /* Allocate source and destination samples buffers */
            int sourceLinesize;
            sourceChannelsCount = ffmpeg.av_get_channel_layout_nb_channels((ulong)sourceChannelLayout);
            fixed(byte*** _source_data = &sourceData)
            {
                ret = ffmpeg.av_samples_alloc_array_and_samples(_source_data, &sourceLinesize, sourceChannelsCount,
                                                         sourceSamplesCount, sourceSampleFormat, 0);
                if (ret < 0)
                {
                    Console.Error.Write("Error. Could not allocate source samples\n");
                    return -1;
                }
            }

            /* Compute the number of converted samples: buffering is avoided
             * ensuring that the output buffer will contain at least all the
             * converted input samples */
            destinationSampleCount =
                (int)ffmpeg.av_rescale_rnd(sourceSamplesCount, destinationSampleRate, sourceSampleRate, AVRounding.AV_ROUND_UP);
            maxDestinationSampleCount = destinationSampleCount;

            /* Buffer is going to be directly written to a rawaudio file, no alignment */
            destinationChanelsCount = ffmpeg.av_get_channel_layout_nb_channels((ulong)destinationChannelLayout);
            fixed(byte*** _dst_data = &destinationData)
            {
                fixed(int* _dst_line_size = &destinationLinesize)
                {
                    ret = ffmpeg.av_samples_alloc_array_and_samples(_dst_data, _dst_line_size, destinationChanelsCount,
                                             destinationSampleCount, destinationSampleFormat, 0);
                    if (ret < 0)
                    {
                        Console.Error.Write("Error. Could not allocate destination samples\n");
                        return -1;
                    }
                }
            }

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

        private unsafe void UninitEncoder()
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

            Console.WriteLine("Info. Uninitialize Encoder.");
        }

        private void UninitResample()
        {
            if(sourceData != null)
            {
                ffmpeg.av_freep(&sourceData[0]);
                fixed(byte*** _src_data = &sourceData)
                {
                    ffmpeg.av_freep(_src_data);
                }
                sourceData = null;
            }

            if(destinationData != null)
            {
                ffmpeg.av_freep(&destinationData[0]);
                fixed(byte*** _dst_data = &destinationData)
                {
                    ffmpeg.av_freep(_dst_data);
                }
                destinationData = null;
            }

            if(resampleContext != null)
            {
                fixed(SwrContext** _resample_context = &resampleContext)
                {
                    ffmpeg.swr_free(_resample_context);
                }
                resampleContext = null;
            }
            Console.WriteLine("Info. Uninitialize Resampler.");
        }

        public unsafe int PushData(byte[] raw_data, int length)
        {
#if FILE_WRITE
            if(wave_file != null)
                wave_file.Write(raw_data, 0, length);
#endif
            Resample(raw_data, length);
            //for (int i = 0; i < length; i++)
                //audio_pcm_datas.Enqueue(data[i]);
            //encode();
            return 0;
        }


        //FillSamples((double*) sourceData[0], sourceSamplesCount, sourceChannelsCount, sourceSampleRate, &toneLevel);
        private void FillSamples(byte* src_data)
        {
            for(int i=0; i < sourceSamplesCount; i++)
            {
                src_data[i] = audio_raw_datas.Dequeue();
            }
        }

        private int Resample(byte[] raw_data, int length)
        {
            for (int i = 0; i < length; i++)
                audio_raw_datas.Enqueue(raw_data[i]);

            while (audio_raw_datas.Count > sourceSamplesCount)
            {
                FillSamples((byte*)sourceData[0]);

                pts = pts + (long)sourceSamplesCount;
                time_scale = codec_context->sample_rate;

                int ret = 0;
                /* Compute destination number of samples */
                destinationSampleCount = (int)ffmpeg.av_rescale_rnd(ffmpeg.swr_get_delay(resampleContext, sourceSampleRate) +
                                                sourceSamplesCount, destinationSampleRate, sourceSampleRate, AVRounding.AV_ROUND_UP);
                if (destinationSampleCount > maxDestinationSampleCount)
                {
                    ffmpeg.av_freep(&destinationData[0]);
                    fixed (int* _dst_line_size = &destinationLinesize)
                    {
                        ret = ffmpeg.av_samples_alloc(destinationData, _dst_line_size, destinationChanelsCount,
                                               destinationSampleCount, destinationSampleFormat, 1);
                    }
                    if (ret < 0)
                    {
                        return -1;
                    }

                    maxDestinationSampleCount = destinationSampleCount;
                }

                /* Convert to destination format */
                ret = ffmpeg.swr_convert(resampleContext, destinationData, destinationSampleCount, sourceData, sourceSamplesCount);
                if (ret < 0)
                {
                    Console.Error.Write("Error while converting\n");
                    return -1;
                }

                /*
                int destinationBufferSize = 0;
                fixed (int* _dst_line_size = &destinationLinesize)
                {
                    destinationBufferSize = ffmpeg.av_samples_get_buffer_size(_dst_line_size, destinationChanelsCount,
                                                             ret, destinationSampleFormat, 1);
                }
                if (destinationBufferSize < 0)
                {
                    Console.Error.Write("Could not get sample buffer size\n");
                    return -1;
                }
                */

                for(int i=0; i< ret; i++)
                {
#if FILE_WRITE
                    if(pcm_file != null)
                        pcm_file.Write(destinationData[0][i]);
#endif
                    audio_resampled_datas.Enqueue(destinationData[0][i]);
                }
                encode(pts);
            }

            return 0;
        }

        private int encode(long pts)
        {
            while (audio_resampled_datas.Count >= data_size)
            {
                byte[] pcm_data = new byte[data_size];
                for (int i = 0; i < data_size; i++)
                    pcm_data[i] = audio_resampled_datas.Dequeue();

                int ret = 0;

                ret = ffmpeg.av_frame_make_writable(frame);
                if (ret < 0)
                {
                    Console.WriteLine("Error. av_frame_make_writable error code[" + ret + "]");
                    return -1;
                }

                fixed (byte* ptr = pcm_data)
                {
                    frame->data[0] = ptr;
                    frame->pts = pts;
                    ret = ffmpeg.avcodec_send_frame(codec_context, frame);
                }
                if (ret < 0)
                {
                    Console.WriteLine("avcodec_send_frame fail [" + ret + "]");
                    return -1;
                }

                while (ret >= 0)
                {
                    ret = ffmpeg.avcodec_receive_packet(codec_context, pkt);
                    if (ret == ffmpeg.AVERROR(ffmpeg.EAGAIN) || ret == ffmpeg.AVERROR_EOF)
                    {
                    }
                    else if (ret < 0)
                    {
                        Console.Error.WriteLine("Error. avcodec_receive_packet fail. error_code[" + ret + "]");
                        return -1;
                    }
                    else
                    {
                        if(GetAACHeader(pkt) == 0)
                        {
                            byte[] data = new byte[pkt->size + aac_adts_header_length];
                            for (int i = 0; i < aac_adts_header_length; i++)
                                data[i] = aac_adts_header[i];

                            for (int i = aac_adts_header_length; i < pkt->size + aac_adts_header_length; i++)
                                data[i] = pkt->data[i - aac_adts_header_length];

                            fixed(byte* _data = data)
                            {
                                rtsp_sender.SendStream(_data, data.Length, pts, pts, time_scale);
                            }

#if FILE_WRITE
                            if(aac_file != null)
                            {
                                //aac_file.Write(aac_adts_header);
                                aac_file.Write(data);
                                aac_file.Flush();
                            }
#endif
                        }
                    }
                    ffmpeg.av_packet_unref(pkt);
                }
            }

            return 0;
        }

        private int GetAACHeader(AVPacket* pkt)
        {
            int profile = 1;
            //int freqIdx = 4;
            //int chanCfg = 1;
            int freqIdx = -1;
            if (codec_context != null)
                freqIdx = GetAACFreqIdx(codec_context->sample_rate);

            int chanCfg = -1;
            if (codec_context != null)
                chanCfg = GetAACChannelConfig(codec_context->channel_layout);

            if(freqIdx == -1)
            {
                Console.Error.WriteLine("Error. Unknown AAC Frequencie.");
                return -1;
            }
            if(chanCfg == -1)
            {
                Console.Error.WriteLine("Error. Unknown AAC Channel Configuration.");
                return -1;
            }

            aac_adts_header[0] = (byte)0xFF;      // 11111111     = syncword  
            aac_adts_header[1] = (byte)0xF1;      // 1111 1 00 1  = syncword MPEG-2 Layer CRC  
            aac_adts_header[2] = (byte)(((profile - 1) << 6) + (freqIdx << 2) + (chanCfg >> 2));
            aac_adts_header[3] = (byte)(((chanCfg & 3) << 6) + ((7 + pkt->size) >> 11));
            aac_adts_header[4] = (byte)(((aac_adts_header_length + pkt->size) & 0x7FF) >> 3);
            aac_adts_header[5] = (byte)((((aac_adts_header_length + pkt->size) & 7) << 5) + 0x1F);
            aac_adts_header[6] = (byte)0xFC;
            return 0;
        }

        //https://wiki.multimedia.cx/index.php?title=MPEG-4_Audio#Sampling_Frequencies
        private int GetAACFreqIdx(int sample_rate)
        {
            if (sample_rate == 96000)
                return 0;
            else if (sample_rate == 88200)
                return 1;
            else if (sample_rate == 64000)
                return 2;
            else if (sample_rate == 48000)
                return 3;
            else if (sample_rate == 44100)
                return 4;
            else if (sample_rate == 32000)
                return 5;
            else if (sample_rate == 24000)
                return 6;
            else if (sample_rate == 22050)
                return 7;
            else if (sample_rate == 16000)
                return 8;
            else if (sample_rate == 12000)
                return 9;
            else if (sample_rate == 11025)
                return 10;
            else if (sample_rate == 8000)
                return 11;
            else if (sample_rate == 7350)
                return 12;
            else
                return -1;
        }

        //https://wiki.multimedia.cx/index.php?title=MPEG-4_Audio#Channel_Configurations
        //https://ffmpeg.org/doxygen/2.4/group__channel__mask__c.html#ga53d6da2dcd56f5f87c7afd8b33fa15ba
        private int GetAACChannelConfig(ulong channels_layout)
        {
            if (channels_layout == ffmpeg.AV_CH_LAYOUT_MONO)
                return 1;
            else if (channels_layout == ffmpeg.AV_CH_LAYOUT_STEREO)
                return 2;
            else if (channels_layout == ffmpeg.AV_CH_LAYOUT_SURROUND)
                return 3;
            else if (channels_layout == ffmpeg.AV_CH_LAYOUT_4POINT0)
                return 4;
            else if (channels_layout == ffmpeg.AV_CH_LAYOUT_5POINT0_BACK)
                return 5;
            else if (channels_layout == ffmpeg.AV_CH_LAYOUT_5POINT1_BACK)
                return 6;
            else if (channels_layout == ffmpeg.AV_CH_LAYOUT_7POINT1_WIDE_BACK)
                return 7;
            else
                return -1;
        }
    }
}
