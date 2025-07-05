using System.Text;

namespace Tinkwell.Cli.Commands;

abstract class Linter
{
    public enum IssueSeverity
    {
        Minor,
        Warning,
        Error,
        Critical,
    }

    public record Issue(string Id, IssueSeverity Severity, string TargetType, string TargetName, string Message);

    public abstract class Rule
    {
        public string Id
            => _id ??= ComputeCrc16(GetType().Name).ToString("00000");

        private string? _id;

        private static ushort ComputeCrc16(string input)
        {
            const ushort poly = 0xA001;
            ushort crc = 0xFFFF;

            foreach (byte b in Encoding.UTF8.GetBytes(input))
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    bool lsb = (crc & 1) == 1;
                    crc >>= 1;
                    if (lsb) crc ^= poly;
                }
            }
            return crc;
        }
    }
}

abstract class Linter<TData> : Linter
{    
    public async Task<IEnumerable<Issue>> CheckAsync(string path)
    {
        TData data = await LoadFileAsync(path);
        List<Issue> issues = new();
        Lint(issues, data);
        return issues
            .OrderByDescending(x => x.Severity)
            .ThenBy(x => x.Id);
    }

    protected abstract Task<TData> LoadFileAsync(string path);

    protected abstract void Lint(IList<Issue> issues, TData data);
}
