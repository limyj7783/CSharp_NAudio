using System;
using System.Text;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
//using System.Threading.Channels;
using System.IO;
using System.Reflection;
//using System.Runtime.Intrinsics.X86;
//using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;

namespace ViewLiveClientMain._2nd_Dev
{
    public enum EN_RTSP_OPT
    {
        EN_RTSP_OPT_TCP_INTERLEAVED = 0,
        EN_RTSP_OPT_UDP
    }

    public enum EN_SUPPORTABLE_CODEC_TYPE
    {
        EN_SUPPORTABLE_CODEC_TYPE_UNKNOWN = 0,
        EN_SUPPORTABLE_CODEC_TYPE_AAC
    }

    public enum EN_LOG_LEVEL_TYPE
    {
        EN_LOG_LEVEL_TYPE_TRACE = 0,
        EN_LOG_LEVEL_TYPE_DEBUG,
        EN_LOG_LEVEL_TYPE_VERBOSE,
        EN_LOG_LEVEL_TYPE_WARN,
        EN_LOG_LEVEL_TYPE_ERR,
        EN_LOG_LEVEL_TYPE_FATAL,
        EN_LOG_LEVEL_TYPE_INFO
    }

    public unsafe class RtspClientWrapper
	{
       
        private AVFormatContext* m_outFormatCtx;
        private string m_rtsp_url = "";
        private int m_ffmpeg_loglevel = ffmpeg.AV_LOG_INFO;
        private Dictionary<int, int> m_map_info = new Dictionary<int, int>();

        private object lockObj = new object();
        private bool is_started_stream;

        public RtspClientWrapper(string rtsp_url, EN_LOG_LEVEL_TYPE loglevel)
		{
            if(!string.IsNullOrEmpty(rtsp_url))
            {
                m_rtsp_url = rtsp_url;
            }
            is_started_stream = false;
            ffmpeg_load(loglevel);
        }

        ~RtspClientWrapper()
        {
            m_map_info.Clear();
            ffmpeg_unload();
        }

        private void ffmpeg_load(EN_LOG_LEVEL_TYPE loglevel)
        {
            ffmpeg.avformat_network_init();

            switch (loglevel)
            {
                case EN_LOG_LEVEL_TYPE.EN_LOG_LEVEL_TYPE_TRACE:
                    m_ffmpeg_loglevel = ffmpeg.AV_LOG_TRACE;
                    break;
                case EN_LOG_LEVEL_TYPE.EN_LOG_LEVEL_TYPE_DEBUG:
                    m_ffmpeg_loglevel = ffmpeg.AV_LOG_DEBUG;
                    break;
                case EN_LOG_LEVEL_TYPE.EN_LOG_LEVEL_TYPE_VERBOSE:
                    m_ffmpeg_loglevel = ffmpeg.AV_LOG_VERBOSE;
                    break;
                case EN_LOG_LEVEL_TYPE.EN_LOG_LEVEL_TYPE_WARN:
                    m_ffmpeg_loglevel = ffmpeg.AV_LOG_WARNING;
                    break;
                case EN_LOG_LEVEL_TYPE.EN_LOG_LEVEL_TYPE_ERR:
                    m_ffmpeg_loglevel = ffmpeg.AV_LOG_ERROR;
                    break;
                case EN_LOG_LEVEL_TYPE.EN_LOG_LEVEL_TYPE_FATAL:
                    m_ffmpeg_loglevel = ffmpeg.AV_LOG_FATAL;
                    break;
                case EN_LOG_LEVEL_TYPE.EN_LOG_LEVEL_TYPE_INFO:
                default:
                    m_ffmpeg_loglevel = ffmpeg.AV_LOG_INFO;
                    break;
            }

            ffmpeg.av_log_set_level(m_ffmpeg_loglevel);

            // do not convert to local function
            av_log_set_callback_callback LogCallback = (p0, level, format, vl) =>
            {
                if (level > ffmpeg.av_log_get_level()) return;

                var lineSize = 2048;
                var lineBuffer = stackalloc byte[lineSize];
                var printPrefix = 1;
                ffmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);

                var line = Marshal.PtrToStringAnsi((IntPtr)lineBuffer);
                Console.ForegroundColor = ConsoleColor.DarkBlue;

                if (level == ffmpeg.AV_LOG_INFO)
                    Console.WriteLine($"rtsp_client_info - {line}");
                else if (level == ffmpeg.AV_LOG_ERROR)
                    Console.WriteLine($"rtsp_client_error - {line}");
                else if (level == ffmpeg.AV_LOG_WARNING)
                    Console.WriteLine($"rtsp_client_warn - {line}");
                else if (level == ffmpeg.AV_LOG_FATAL)
                    Console.WriteLine($"rtsp_client_fatal - {line}");
                else if (level == ffmpeg.AV_LOG_DEBUG)
                    Console.WriteLine($"rtsp_client_debug - {line}");
                else if (level == ffmpeg.AV_LOG_VERBOSE)
                    Console.WriteLine($"rtsp_client_verbose - {line}");
                else if (level == ffmpeg.AV_LOG_TRACE)
                    Console.WriteLine($"rtsp_client_trace - {line}");
                else
                    Console.WriteLine($"rtsp_client -  {line}");
                Console.ResetColor();
            };
            ffmpeg.av_log_set_callback(LogCallback);

            Console.WriteLine($" #### set_level: {m_ffmpeg_loglevel} get_level: {ffmpeg.av_log_get_level()}");
        }

        private void ffmpeg_unload()
        {
            ffmpeg.avformat_network_deinit();
        }
    

        private int ffmpeg_rtsp_client_init(string rtsp_url)
        {
            // 여기 내부에서  
            if (rtsp_url.Length == 0)
            {
                Console.WriteLine("Invalid rtsp_url");
                return -1;
            }
            int ret = 0;

            fixed (AVFormatContext** _outCtx = &m_outFormatCtx)
            {
                ret = ffmpeg.avformat_alloc_output_context2(_outCtx, null, "rtsp", rtsp_url);
                if (ret < 0)
                {
                    byte[] errstring = new byte[ffmpeg.AV_ERROR_MAX_STRING_SIZE];
                    fixed (byte* _buf = &errstring[0])
                    {
                        byte[] arr = new byte[ffmpeg.AV_ERROR_MAX_STRING_SIZE];
                        Marshal.Copy((IntPtr)ffmpeg.av_make_error_string(_buf, ffmpeg.AV_ERROR_MAX_STRING_SIZE, ret), arr, 0, ffmpeg.AV_ERROR_MAX_STRING_SIZE);
                        Console.WriteLine($"failed to avformat_alloc_output_context2 - {Encoding.ASCII.GetString(arr)}({ret}) ");
                        return -1;
                    }
                }
            }

            return ret;
        }

        public int AddAudioStream(int input_stream_index, EN_SUPPORTABLE_CODEC_TYPE codec, int sample_rate, int channel, int bits_aligned, byte* extra_data, int extra_data_size)
        {
            lock (lockObj)
            {

                if (codec == EN_SUPPORTABLE_CODEC_TYPE.EN_SUPPORTABLE_CODEC_TYPE_UNKNOWN || sample_rate <= 0 || channel <= 0)
                {
                    Console.WriteLine("AddAudioStream. Invalid Input Parameters ");
                    return -1;
                }

                if (m_outFormatCtx == null)
                {
                    int ret = ffmpeg_rtsp_client_init(m_rtsp_url);
                    if (ret < 0) return ret;

                    if (m_map_info.Count > 0) m_map_info.Clear();
                }

                AVCodecID input_codec = AVCodecID.AV_CODEC_ID_NONE;
                AVMediaType media_type = AVMediaType.AVMEDIA_TYPE_UNKNOWN;
                switch (codec)
                {
                    case EN_SUPPORTABLE_CODEC_TYPE.EN_SUPPORTABLE_CODEC_TYPE_AAC:
                        input_codec = AVCodecID.AV_CODEC_ID_AAC;
                        media_type = AVMediaType.AVMEDIA_TYPE_AUDIO;
                        break;
                    default:
                        Console.WriteLine($"Not supported codec type - {codec}");
                        break;
                }
                if (input_codec == AVCodecID.AV_CODEC_ID_NONE) return -3;


                // find the mapping information..
                if (m_map_info.ContainsKey(input_stream_index))
                {
                    Console.WriteLine($"already added stream... ({input_stream_index} --> {m_map_info[input_stream_index]})");
                    return -4;
                }

                AVOutputFormat* fmt = m_outFormatCtx->oformat;
                AVStream* st = ffmpeg.avformat_new_stream(m_outFormatCtx, null);
                if (st == null)
                {
                    Console.WriteLine($" failed to allocating output stream ");
                    return -5;
                }

                st->codecpar->codec_id = input_codec;
                if (media_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                {
                    st->codecpar->codec_type = AVMediaType.AVMEDIA_TYPE_AUDIO;
                    st->codecpar->sample_rate = sample_rate;
                    st->codecpar->channels = channel;
                    st->time_base.den = sample_rate;
                    st->time_base.num = 1;
                    if (extra_data != null && extra_data_size > 0)
                    {
                        st->codecpar->extradata = (byte*)ffmpeg.av_mallocz((ulong)(extra_data_size + ffmpeg.AV_INPUT_BUFFER_PADDING_SIZE));
                        if (st->codecpar->extradata != null)
                        {
                            Buffer.MemoryCopy(extra_data, st->codecpar->extradata, extra_data_size + ffmpeg.AV_INPUT_BUFFER_PADDING_SIZE, extra_data_size);
                            st->codecpar->extradata_size = extra_data_size;
                        }
                    }
                }

                m_map_info.Add(input_stream_index, (int)m_outFormatCtx->nb_streams - 1);
                Console.WriteLine($" # of output streams: {m_outFormatCtx->nb_streams} new stream index: {m_outFormatCtx->nb_streams - 1}");
                return 0;
            }
        }


        public int StartStream(EN_RTSP_OPT opt /*= EN_RTSP_OPT_TCP_INTERLEAVED */, int timeout_msec /*= 5000*/)
        {
            Console.WriteLine($"StartStream called");

            lock (lockObj)
            {
                /* init muxer, write output file header */
                int ret = 0;
                AVDictionary* dicts = null;

                if (opt == EN_RTSP_OPT.EN_RTSP_OPT_TCP_INTERLEAVED)
                {
                    ret = ffmpeg.av_dict_set(&dicts, "rtsp_transport", "tcp", 0);
                    if (ret >= 0)
                    {
                        Console.WriteLine($"tcp_interleaved transport option is ON");
                    }
                    else
                    {
                        byte[] errorBuff = new byte[ffmpeg.AV_ERROR_MAX_STRING_SIZE];
                        fixed (byte* _buf = &errorBuff[0])
                        {
                            var line = Marshal.PtrToStringAnsi((IntPtr)ffmpeg.av_make_error_string(_buf, ffmpeg.AV_ERROR_MAX_STRING_SIZE, ret));
                            Console.WriteLine($" failed to set tcp_interleaved option -  {line}, {ret}");
                            return -1;
                        }
                    }

                    int time = timeout_msec * 1000;
                    ret = ffmpeg.av_dict_set(&dicts, "stimeout", time.ToString(), 0);
                    if (ret >= 0)
                    {
                        Console.WriteLine($"tcp_timeout option is ON");
                    }
                    else
                    {
                        byte[] errorBuff = new byte[ffmpeg.AV_ERROR_MAX_STRING_SIZE];
                        fixed (byte* _buf = &errorBuff[0])
                        {
                            var line = Marshal.PtrToStringAnsi((IntPtr)ffmpeg.av_make_error_string(_buf, ffmpeg.AV_ERROR_MAX_STRING_SIZE, ret));
                            Console.WriteLine($" failed to set tcp_timeout option -  {line}, {ret}");
                            return -1;
                        }
                    }

                    /*
                    m_outFormatCtx->interrupt_callback.callback = CheckInterrupt;
                    m_outFormatCtx->interrupt_callback.opaque = this;
                    */
                }
                else
                {
                    Console.WriteLine($"udp transport option is ON");
                }

                Console.WriteLine($"avformat_write_header before");
                ret = ffmpeg.avformat_write_header(m_outFormatCtx, &dicts);
                Console.WriteLine($"avformat_write_header after");
                if (ret < 0)
                {
                    byte[] errorBuff = new byte[ffmpeg.AV_ERROR_MAX_STRING_SIZE];
                    fixed (byte* _buf = &errorBuff[0])
                    {
                        var line = Marshal.PtrToStringAnsi((IntPtr)ffmpeg.av_make_error_string(_buf, ffmpeg.AV_ERROR_MAX_STRING_SIZE, ret));
                        Console.WriteLine($" Error occured when opening output stream - {line}, {ret}");
                        return ret;
                    }
                }

                is_started_stream = true;
            }

            return 0;
        }

        public int SendStream(int index, EN_SUPPORTABLE_CODEC_TYPE codec, byte* data, int size, Int64 pts, Int64 dts, int timescale)
        {
            // Console.WriteLine($" index: {index} pts : {pts} timescale : {timescale} ");

            lock (lockObj)
            {
                int output_index = -1;
                if (!m_map_info.ContainsKey(index))
                {
                    Console.WriteLine($" Invalid input parameter... index: {index}");
                    return -1;
                }
                else
                {
                    output_index = m_map_info[index];
                    if (output_index < 0) return -2;
                }

                if (m_outFormatCtx->streams[output_index]->time_base.den != timescale)
                {
                    Console.WriteLine($"rescaling the pts is requested.( {timescale} --> {m_outFormatCtx->streams[index]->time_base.den})");
                    return -3;
                }

                AVPacket* pkt = ffmpeg.av_packet_alloc();
                if (pkt == null)
                {
                    Console.WriteLine($"failed to allocating packet ");
                    return -4;
                }


                byte* _buf = (byte*)ffmpeg.av_malloc((ulong)size);
                Buffer.MemoryCopy(data, _buf, size, size);
                int ret = ffmpeg.av_packet_from_data(pkt, (byte*)_buf, size);
                if (ret < 0)
                {
                    byte[] errorBuff = new byte[ffmpeg.AV_ERROR_MAX_STRING_SIZE];
                    fixed (byte* _err = &errorBuff[0])
                    {
                        var line = Marshal.PtrToStringAnsi((IntPtr)ffmpeg.av_make_error_string(_err, ffmpeg.AV_ERROR_MAX_STRING_SIZE, ret));
                        Console.WriteLine($"failed to av_packet_from_data - {line}, ({ret})");
                    }

                    ffmpeg.av_packet_unref(pkt);
                    ffmpeg.av_packet_free(&pkt);
                    return ret;
                }

                pkt->pts = pts;
                pkt->dts = dts;
                pkt->stream_index = output_index;

                ret = ffmpeg.av_interleaved_write_frame(m_outFormatCtx, pkt);
                if (ret < 0)
                {
                    byte[] errorBuff = new byte[ffmpeg.AV_ERROR_MAX_STRING_SIZE];
                    fixed (byte* _err = &errorBuff[0])
                    {
                        var line = Marshal.PtrToStringAnsi((IntPtr)ffmpeg.av_make_error_string(_err, ffmpeg.AV_ERROR_MAX_STRING_SIZE, ret));
                        Console.WriteLine($" failed to av_interleaved_write_frame - {line}, {ret}");
                       

                        ffmpeg.av_packet_unref(pkt);
                        ffmpeg.av_packet_free(&pkt);
                        return ret;
                    }
                }

                ffmpeg.av_packet_unref(pkt);
                ffmpeg.av_packet_free(&pkt);
            }
            return 0;
        }

        public int SendStream(AVPacket* pkt, int timescale)
        {
            // Console.WriteLine($" index: {pkt->stream_index} pts : {pkt->pts} timescale : {timescale} ");

            lock (lockObj)
            {
                if (!m_map_info.ContainsKey(pkt->stream_index))
                {
                    Console.WriteLine($" Invalid input parameter... index: {pkt->stream_index}");
                    return -1;
                }
                else
                {
                    pkt->stream_index = m_map_info[pkt->stream_index];
                }

                if (m_outFormatCtx->streams[m_map_info[pkt->stream_index]]->time_base.den != timescale)
                {
                    Console.WriteLine($"rescaling the pts is requested.( {timescale} --> {m_outFormatCtx->streams[m_map_info[pkt->stream_index]]->time_base.den})");
                    return -2;
                }

                int ret = ffmpeg.av_interleaved_write_frame(m_outFormatCtx, pkt);
                if (ret < 0)
                {
                    byte[] errorBuff = new byte[ffmpeg.AV_ERROR_MAX_STRING_SIZE];
                    fixed (byte* _err = &errorBuff[0])
                    {
                        var line = Marshal.PtrToStringAnsi((IntPtr)ffmpeg.av_make_error_string(_err, ffmpeg.AV_ERROR_MAX_STRING_SIZE, ret));
                        Console.WriteLine($" failed to av_interleaved_write_frame - {line}, {ret}");
                        return ret;
                    }
                }
            }
            return 0;
        }

        public void StopStream()
        {
            Console.WriteLine($"StopStream called");

            lock (lockObj)
            {
                if (m_map_info.Count > 0)
                    m_map_info.Clear();

                is_started_stream = false;

                ffmpeg_rtsp_client_deinit();
            }
        }

        private void ffmpeg_rtsp_client_deinit()
        {
            if (m_outFormatCtx != null)
            {
                int ret = ffmpeg.av_write_trailer(m_outFormatCtx);
                if (ret != 0)
                {

                    byte[] errorBuff = new byte[ffmpeg.AV_ERROR_MAX_STRING_SIZE];
                    fixed (byte* _err = &errorBuff[0])
                    {
                        var line = Marshal.PtrToStringAnsi((IntPtr)ffmpeg.av_make_error_string(_err, ffmpeg.AV_ERROR_MAX_STRING_SIZE, ret));
                        Console.WriteLine($" failed to av_write_trailer - {line}, {ret}");
                    }
                }
                ffmpeg.avformat_free_context(m_outFormatCtx);
                m_outFormatCtx = null;
            }
        }

        public bool IsStartedStream()
        {
            return is_started_stream;
        }
    }
}

