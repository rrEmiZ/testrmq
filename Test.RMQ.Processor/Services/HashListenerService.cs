using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Test.RMQ.Common;

namespace Test.RMQ.Processor.Services
{
    public class HashListenerService : BackgroundService
    {
        private const string QUEUE_NAME = "hashes";

        private const int WORKER_THREADS = 4;

        private readonly ConnectionFactory _connectionFactory;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _connectionString; //dbconnection string
        private readonly BlockingCollection<HashMessage> _messageQueue;

        public HashListenerService(string host, int port, string username, string password, string connectionString)
        {
            _connectionFactory = new ConnectionFactory()
            {
                HostName = host,
                Port = port,
                UserName = username,
                Password = password
            };

            _connection = _connectionFactory.CreateConnection();

            var args = new Dictionary<string, object>()
                        {
                            { "x-queue-type", typeof(HashMessage).FullName }
                        };

            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: QUEUE_NAME,
                  durable: false,
                  exclusive: false,
                  autoDelete: false,
                  arguments: args
             );


            _connectionString = connectionString;

            _messageQueue = new BlockingCollection<HashMessage>();
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            PrepareMessageConsumption();

            var tasks = new Task[WORKER_THREADS];
            for (int i = 0; i < WORKER_THREADS; i++)
            {
                tasks[i] = Task.Run(async () => await ProcessMessages(stoppingToken));
            }

            await Task.WhenAll(tasks);
        }

        private async Task ProcessMessages(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_messageQueue.TryTake(out var item))
                    {
                        await ProcessMessageAsync(item.Sha1);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }


        private async Task ProcessMessageAsync(string message)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync("INSERT INTO Hashes (Date, SHA1) VALUES (@Date, @SHA1)",
                    new { Date = DateTime.UtcNow, SHA1 = message });
            }
        }

        private void PrepareMessageConsumption()
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (sender, args) =>
            {
                var message = JsonConvert.DeserializeObject<HashMessage>(Encoding.UTF8.GetString(args.Body.ToArray()));

                _messageQueue.Add(message);

                _channel.BasicAck(args.DeliveryTag, multiple: false);
            };

            _channel.BasicConsume(queue: QUEUE_NAME, autoAck: false, consumer: consumer);
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _messageQueue.CompleteAdding();

            _channel.Close();
            _connection.Close();

            await base.StopAsync(stoppingToken);
        }
    }
}
