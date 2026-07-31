using XianXia.Core.Results;

namespace XianXia.Core.Persistence
{
    public interface ISnapshotSerializer
    {
        Result<string> Serialize(WorldSnapshot snapshot);

        Result<WorldSnapshot> Deserialize(string json);
    }
}
