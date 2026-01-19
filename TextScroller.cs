using System.Collections.Concurrent;

namespace Menu
{
    /// <summary>
    /// Handles text scrolling animation for long menu items.
    /// Text scrolls from right to left when it exceeds max width.
    /// </summary>
    public static class TextScroller
    {
        // Track scroll offsets per unique text
        private static readonly ConcurrentDictionary<string, int> _scrollOffsets = new();
        
        // Track tick counter for scroll speed control
        private static readonly ConcurrentDictionary<string, int> _tickCounters = new();
        
        // Track pause state - holds after completing a full cycle
        private static readonly ConcurrentDictionary<string, int> _pauseCounters = new();
        
        // Max characters to display
        private const int MaxDisplayChars = 20;
        
        // Ticks between each scroll step (higher = slower)
        private const int TicksPerScroll = 8;
        
        // Ticks to pause after completing one full scroll cycle (2-3 seconds at ~64 ticks/sec)
        private const int PauseTicks = 160; // ~2.5 seconds
        
        /// <summary>
        /// Gets scrolled text for display. Call this every tick for animation.
        /// </summary>
        /// <param name="text">Original text</param>
        /// <param name="maxChars">Maximum visible characters</param>
        /// <returns>Scrolled portion of text</returns>
        public static string GetScrolledText(string text, int maxChars = MaxDisplayChars)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            // Strip HTML for length calculation
            var plainText = StripHtml(text);
            
            // If text fits, no scrolling needed
            if (plainText.Length <= maxChars)
                return text;
            
            // Get or create scroll offset for this text
            var key = text.GetHashCode().ToString();
            
            // Check if we're in pause mode
            var pauseCount = _pauseCounters.GetOrAdd(key, 0);
            if (pauseCount > 0)
            {
                // Decrement pause counter
                _pauseCounters[key] = pauseCount - 1;
                
                // Show text from beginning during pause
                var currentOffset = _scrollOffsets.GetOrAdd(key, 0);
                return GetVisiblePortion(plainText, currentOffset, maxChars);
            }
            
            // Update tick counter
            var tickCount = _tickCounters.AddOrUpdate(key, 1, (_, v) => v + 1);
            
            // Only scroll every N ticks
            if (tickCount >= TicksPerScroll)
            {
                _tickCounters[key] = 0;
                
                // Calculate max offset for one complete cycle
                var maxOffset = plainText.Length + 3; // +3 for spacing between loops
                var currentOffset = _scrollOffsets.GetOrAdd(key, 0);
                var newOffset = currentOffset + 1;
                
                // Check if we completed a full cycle
                if (newOffset >= maxOffset)
                {
                    // Reset to beginning and enter pause mode
                    _scrollOffsets[key] = 0;
                    _pauseCounters[key] = PauseTicks;
                }
                else
                {
                    _scrollOffsets[key] = newOffset;
                }
            }
            
            var offset = _scrollOffsets.GetOrAdd(key, 0);
            
            // Calculate visible portion
            var displayText = GetVisiblePortion(plainText, offset, maxChars);
            
            return displayText;
        }
        
        /// <summary>
        /// Gets the visible portion of text based on scroll offset.
        /// </summary>
        private static string GetVisiblePortion(string text, int offset, int maxChars)
        {
            // Add padding at the end for smooth scroll loop
            var paddedText = text + "   ";
            
            // Calculate start position with wrap-around
            var start = offset % paddedText.Length;
            
            // Extract visible portion
            if (start + maxChars <= paddedText.Length)
            {
                return paddedText.Substring(start, maxChars);
            }
            else
            {
                // Wrap around to beginning
                var part1 = paddedText.Substring(start);
                var remaining = maxChars - part1.Length;
                
                // Show beginning of text for wrap-around
                var part2 = text.Substring(0, Math.Min(remaining, text.Length));
                return part1 + part2;
            }
        }
        
        /// <summary>
        /// Strips HTML tags from text.
        /// </summary>
        private static string StripHtml(string text)
        {
            return System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", "");
        }
        
        /// <summary>
        /// Clears all scroll state (call when menu closes).
        /// </summary>
        public static void Reset()
        {
            _scrollOffsets.Clear();
            _tickCounters.Clear();
            _pauseCounters.Clear();
        }
        
        /// <summary>
        /// Clears scroll state for a specific text.
        /// </summary>
        public static void ResetText(string text)
        {
            var key = text.GetHashCode().ToString();
            _scrollOffsets.TryRemove(key, out _);
            _tickCounters.TryRemove(key, out _);
            _pauseCounters.TryRemove(key, out _);
        }
    }
}