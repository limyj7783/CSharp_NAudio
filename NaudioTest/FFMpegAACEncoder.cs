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
        public bool isInit;

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

        BinaryWriter bw = new BinaryWriter(new FileStream(@"C:\Project\CSharp_NAudio\resampled.pcm", FileMode.Create, FileAccess.Write));
        BinaryWriter aac_file = new BinaryWriter(new FileStream(@"C:\Project\CSharp_NAudio\output.aac", FileMode.Create, FileAccess.Write));
        struct sample_fmt_entry
        {
            public AVSampleFormat sample_fmt;
            public string fmt_be, fmt_le;
        }

        public FFMpegAACEncoder()
        {
            isInit = false;

            codec = null;
            codec_context = null;
            frame = null;
            pkt = null;
            data_size = 0;
            audio_resampled_datas = new Queue<byte>();
            audio_raw_datas = new Queue<byte>();
            aac_adts_header = new byte[7];

            resampleContext = null;
            sourceData = null;
            destinationData = null;
            sourceChannelLayout = (long)ffmpeg.AV_CH_LAYOUT_MONO;
            destinationChannelLayout = (long)ffmpeg.AV_CH_LAYOUT_MONO;
            sourceSampleRate = 44100;
            destinationSampleRate = 44100;
            sourceSampleFormat = AVSampleFormat.AV_SAMPLE_FMT_S32;
            destinationSampleFormat = AVSampleFormat.AV_SAMPLE_FMT_FLTP;
            sourceSamplesCount = 1024;
            sourceChannelsCount = 0;

            destinationSampleCount = 0;
            maxDestinationSampleCount = 0;
            destinationLinesize = 0;
            destinationChanelsCount = 0;

            if (InitEncoder() < 0)
                UninitEncoder();
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
            codec_context->channel_layout = ffmpeg.AV_CH_LAYOUT_MONO;// SelectChannelLayout(codec);
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

            if (InitResample() < 0)
                UninitResample();

            isInit = true;
            return 0;
        }

        public int InitResample()
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

            UninitResample();

            isInit = false;
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
        }

        public unsafe int PushData(byte[] raw_data, int length)
        {
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
                    bw.Write(destinationData[0][i]);
                    audio_resampled_datas.Enqueue(destinationData[0][i]);
                }
                encode();
            }

            return 0;
        }

        //private bool encode(AVCodecContext* ctx, AVFrame* frame, AVPacket* pkt)
        private int encode()
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
                        Console.WriteLine("--------->");
                    }
                    else if (ret < 0)
                    {
                        Console.Error.WriteLine("Error. avcodec_receive_packet fail. error_code[" + ret + "]");
                        return -1;
                    }
                    else
                    {
                        GetAACHeader(pkt);

                        byte[] data = new byte[pkt->size];
                        for (int i = 0; i < pkt->size; i++)
                            data[i] = pkt->data[i];

                        aac_file.Write(aac_adts_header);
                        aac_file.Write(data);
                        aac_file.Flush();

                        ffmpeg.av_packet_unref(pkt);
                    }
                }
            }

            return 0;
        }

        private int GetAACHeader(AVPacket* pkt)
        {
            int profile = 1;
            int freqIdx = 4;
            int chanCfg = 1;        //MPEG-4 Audio Channel Configuration. 1 Channel front-center

            aac_adts_header[0] = (byte)0xFF;      // 11111111     = syncword  
            aac_adts_header[1] = (byte)0xF1;      // 1111 1 00 1  = syncword MPEG-2 Layer CRC  
            aac_adts_header[2] = (byte)(((profile - 1) << 6) + (freqIdx << 2) + (chanCfg >> 2));
            aac_adts_header[3] = (byte)(((chanCfg & 3) << 6) + ((7 + pkt->size) >> 11));
            aac_adts_header[4] = (byte)(((7 + pkt->size) & 0x7FF) >> 3);
            aac_adts_header[5] = (byte)((((7 + pkt->size) & 7) << 5) + 0x1F);
            aac_adts_header[6] = (byte)0xFC;
            return 0;
        }

        static int getFormatFromSampleFormat(out string fmt, AVSampleFormat sample_fmt)
        {
            var sample_fmt_entries = new[]{
                new sample_fmt_entry{ sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_U8,  fmt_be = "u8",    fmt_le = "u8"    },
                new sample_fmt_entry{ sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_S16, fmt_be = "s16be", fmt_le = "s16le" },
                new sample_fmt_entry{ sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_S32, fmt_be = "s32be", fmt_le = "s32le" },
                new sample_fmt_entry{ sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_FLT, fmt_be = "f32be", fmt_le = "f32le" },
                new sample_fmt_entry{ sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_DBL, fmt_be = "f64be", fmt_le = "f64le" },
            };
            fmt = null;
            for (var i = 0; i < sample_fmt_entries.Length; i++)
            {
                var entry = sample_fmt_entries[i];
                if (sample_fmt == entry.sample_fmt)
                {
                    fmt = ffmpeg.AV_HAVE_BIGENDIAN != 0 ? entry.fmt_be : entry.fmt_le;
                    return 0;
                }
            }

            Console.Error.WriteLine($"Sample format {ffmpeg.av_get_sample_fmt_name(sample_fmt)} not supported as output format");
            return ffmpeg.AVERROR(ffmpeg.EINVAL);
        }
    }
}
