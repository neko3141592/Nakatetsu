namespace Nakatetsu.TrafficControl.Server.CTC
{
    public static class CTCLogic
    {
        public static void Calculate(CTCContext context)
        {
            // 現段階では受信状態をそのまま公開する。未受信はnullを維持する。
            context.Output = context.Input;
        }
    }
}
