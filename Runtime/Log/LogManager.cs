using System.Collections.Concurrent;

namespace CFramework.Core.Log
{
    public class LogManager
    {
        private readonly ConcurrentDictionary<string, CFLogger> _loggerDict = new();
        public readonly CFLogger CFLogger = new CFLogger(nameof(CFramework));
        private volatile ICFLogger.Level _level = ICFLogger.Level.Debug;
        private volatile bool _enabled = true;

        public ICFLogger.Level Level => _level;
        public bool Enabled => _enabled;

        public CFLogger Create(string tag, bool enabled = true)
        {
            CFLogger logger = new CFLogger(tag);
            logger.SetEnabled(enabled);
            logger.SetLevel(Level);
            RegisterLogger(tag, logger);
            return logger;
        }

        public void RegisterLogger(string tag, CFLogger logger)
        {
            _loggerDict.AddOrUpdate(tag, logger, (key, oldLogger) =>
            {
                oldLogger.SetEnabled(false);
                return logger;
            });
        }

        public void SetLevel(string tag, ICFLogger.Level level)
        {
            if (_loggerDict.TryGetValue(tag, out var logger))
            {
                logger.SetLevel(level);
            }
            else if (tag.Equals(nameof(CFramework)))
            {
                CFLogger.SetLevel(level);
            }
        }

        public void SetLevelAll(ICFLogger.Level level)
        {
            _level = level;
            CFLogger.SetLevel(level);
            foreach (CFLogger logger in _loggerDict.Values)
            {
                logger.SetLevel(level);
            }
        }

        public void SetEnabled(string tag, bool enabled)
        {
            if (_loggerDict.TryGetValue(tag, out var logger))
            {
                logger.SetEnabled(enabled);
            }
            else if (tag.Equals(nameof(CFramework)))
            {
                CFLogger.SetEnabled(enabled);
            }
        }

        public void SetEnabledAll(bool enabled)
        {
            _enabled = enabled;
            CFLogger.SetEnabled(enabled);
            foreach (CFLogger logger in _loggerDict.Values)
            {
                logger.SetEnabled(enabled);
            }
        }

        public CFLogger GetLogger(string tag)
        {
            if (tag.Equals(nameof(CFramework)))
            {
                return CFLogger;
            }
            _loggerDict.TryGetValue(tag, out var logger);
            return logger;
        }

        public string[] GetAllTags()
        {
            var tags = new string[_loggerDict.Count + 1];
            tags[0] = nameof(CFramework);
            _loggerDict.Keys.CopyTo(tags, 1);
            return tags;
        }

        public bool RemoveLogger(string tag)
        {
            return _loggerDict.TryRemove(tag, out _);
        }

        public void Clear()
        {
            _loggerDict.Clear();
        }
    }
}