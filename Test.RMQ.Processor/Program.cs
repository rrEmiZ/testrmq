// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Test.RMQ.Processor.Services;

Console.WriteLine("Worker started");


var host = new HostBuilder()
          .ConfigureHostConfiguration(configHost =>
          {

          })
          .ConfigureAppConfiguration((hostContext, cfg) =>
          {
              cfg.AddJsonFile("appsettings.json");
          })
          .ConfigureServices((hostContext, services) =>
          {

              services.AddHostedService<HashListenerService>((provider) =>
              {
                  var cfg = provider.GetRequiredService<IConfiguration>();

                  var rMQSection = cfg.GetSection("rabbitmq");
                  var hostName = rMQSection.GetValue<string>("host");
                  var port = rMQSection.GetValue<int>("port");
                  var userName = rMQSection.GetValue<string>("username");
                  var password = rMQSection.GetValue<string>("password");
                  var dbConnectionString = cfg.GetConnectionString("DefaultConnection");
                  return new HashListenerService(hostName, port, userName, password, dbConnectionString);
              });
          })
         .UseConsoleLifetime()
         .Build();

//run
await host.RunAsync();