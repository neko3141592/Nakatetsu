using Nakatetsu.TrafficControl.Server.CTC;

namespace Nakatetsu.TrafficControl.Server.Communication.Web
{
    public static class CTCEndpoint
    {
        public static void MapRoutes(IEndpointRouteBuilder endpoints)
        {
            var group = endpoints.MapGroup("/api/ctc");
            group.MapGet("/snapshot", GetSnapshot);
        }

        public static IResult GetSnapshot(CTCController ctcController)
        {
            var message = ctcController.GetSnapshot();
            if (message is null)
            {
                return Results.Problem(
                    detail: "Unityの状態をまだ受信していません。",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(message);
        }
    }
}
