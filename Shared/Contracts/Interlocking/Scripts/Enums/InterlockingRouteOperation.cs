namespace Nakatetsu.Contracts.Interlocking
{
    public enum InterlockingRouteOperation
    {
        // 未指定。要求を処理する側でInvalidRequestとして拒否する。
        None = 0,
        Set = 1,
        Cancel = 2
    }
}
