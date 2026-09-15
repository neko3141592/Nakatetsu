namespace Nakatetsu.Train.Equipment.Traction
{
    public interface ITractionCommandSource
    {
        bool TryGetTargetTractionForceN(out float targetTractionForceN);
    }

    // 力行/回生の指令符号とは別に、編成上の駆動方向を渡す。
    public interface ITractionDirectionSource
    {
        bool TryGetTractionDirection(out int directionSign);
    }
}
