using Test.RMQ.API.Services;

namespace Test.RMQ.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();

            builder.Services.AddTransient<IHashService, HashService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseAuthorization();

            app.MapPost("/hashes", async (HttpContext httpContext, IHashService hashService, CancellationToken token) =>
            {
                try
                {
                     hashService.GenerateAndSendHashesAsync(token);
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }

                return Results.Ok("GeneratedAndSend");
            });

            app.MapGet("/hashes", async (HttpContext httpContext, IHashService hashService) =>
            {
                try
                {
                    return Results.Ok(await hashService.GetHashStatisticsAsync());
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            app.Run();
        }
    }
}