using System;
using System.Collections.Generic;
using youtube_dl_gui.Youtube;

namespace youtube_dl_gui
{
    class YoutubeVideoFormatComparer : IComparer<YoutubeVideoFormat>
    {
        class RevertYoutubeVideoFormatComparer : YoutubeVideoFormatComparer
        {
            internal RevertYoutubeVideoFormatComparer() : base() { }

            public override int Compare(YoutubeVideoFormat left, YoutubeVideoFormat right) => -base.Compare(left, right);
        }
        public static readonly YoutubeVideoFormatComparer Default = new YoutubeVideoFormatComparer();
        public static readonly YoutubeVideoFormatComparer Revert = new RevertYoutubeVideoFormatComparer();

        private YoutubeVideoFormatComparer() { }

        public virtual int Compare(YoutubeVideoFormat left, YoutubeVideoFormat right)
        {
            bool left_hasVideo = (left.VideoCodec != null),
                right_hasVideo = (right.VideoCodec != null);

            if (GetFriendlyFormatExtension(left) == GetFriendlyFormatExtension(right))
            {
                if (left_hasVideo && right_hasVideo)
                {
                    double left_height = left.VideoResolution.Height,
                        right_height = right.VideoResolution.Height;

                    if (left_height == right_height)
                    {
                        int leftFPS = left.FPS.HasValue ? left.FPS.Value : 30,
                            rightFPS = right.FPS.HasValue ? right.FPS.Value : 30;
                        if (leftFPS == rightFPS)
                        {
                            int left_priority = GetFormatPriority(left),
                                right_priority = GetFormatPriority(right);
                            if (left_priority == right_priority)
                            {
                                return 0;
                            }
                            else if (left_priority > right_priority)
                            {
                                return 1;
                            }
                            else
                            {
                                return -1;
                            }
                        }
                        else if (leftFPS > rightFPS)
                        {
                            return 1;
                        }
                        else
                        {
                            return -1;
                        }
                    }
                    else if (left_height > right_height)
                    {
                        return 1;
                    }
                    else
                    {
                        return -1;
                    }
                }
                else if (!left_hasVideo && !right_hasVideo)
                {
                    int left_audiobitrate = left.AudioBirate.Value,
                        right_audiobitrate = right.AudioBirate.Value;

                    if (left_audiobitrate == right_audiobitrate)
                    {
                        int left_priority = GetFormatPriority(left),
                          right_priority = GetFormatPriority(right);
                        if (left_priority == right_priority)
                        {
                            return 0;
                        }
                        else if (left_priority > right_priority)
                        {
                            return 1;
                        }
                        else
                        {
                            return -1;
                        }
                    }
                    else if (left_audiobitrate > right_audiobitrate)
                    {
                        return 1;
                    }
                    else
                    {
                        return -1;
                    }
                }
                else
                {
                    if (left_hasVideo)
                    {
                        return 1;
                    }
                    else
                    {
                        return -1;
                    }
                }
            }
            else
            {
                int left_priority = GetFormatPriority(left),
                    right_priority = GetFormatPriority(right);
                if (left_priority == right_priority)
                {
                    return 0;
                }
                else if (left_priority > right_priority)
                {
                    return 1;
                }
                else
                {
                    return -1;
                }
            }
        }

        internal static int GetFormatPriority(YoutubeVideoFormat format)
        {
            string formatString = format.VideoCodec ?? format.AudioCodec;
            if (formatString.Equals("opus", StringComparison.OrdinalIgnoreCase))
            {
                return -3;
            }
            else if (formatString.Equals("vorbis", StringComparison.OrdinalIgnoreCase))
            {
                return -5;
            }
            else if (formatString.Equals("vp9", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            else if (formatString.Equals("vp8.0", StringComparison.OrdinalIgnoreCase) || formatString.Equals("vp8", StringComparison.OrdinalIgnoreCase))
            {
                return -2;
            }
            else if (formatString.StartsWith("avc1", StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }
            else if (formatString.StartsWith("mp4a", StringComparison.OrdinalIgnoreCase))
            {
                return -4;
            }
            else
            {
                return 0;
            }
        }

        internal static string GetFriendlyFormatExtension(YoutubeVideoFormat format)
        {
            string formatString = format.VideoCodec ?? format.AudioCodec;
            if (formatString.Equals("opus", StringComparison.OrdinalIgnoreCase))
            {
                return "Opus";
            }
            else if (formatString.Equals("vorbis", StringComparison.OrdinalIgnoreCase))
            {
                return "Vorbis";
            }
            else if (formatString.Equals("vp9", StringComparison.OrdinalIgnoreCase))
            {
                return "VP9";
            }
            else if (formatString.Equals("vp8.0", StringComparison.OrdinalIgnoreCase) || formatString.Equals("vp8", StringComparison.OrdinalIgnoreCase))
            {
                return "VP8";
            }
            else if (formatString.StartsWith("avc1", StringComparison.OrdinalIgnoreCase))
            {
                return "H264";
            }
            else if (formatString.StartsWith("mp4a", StringComparison.OrdinalIgnoreCase))
            {
                return "M4A";
            }
            else
            {
                return formatString.FirstLetterToUpper();
            }
        }
    }
}
