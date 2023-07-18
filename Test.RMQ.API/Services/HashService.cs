using Dapper;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Test.RMQ.Common;

namespace Test.RMQ.API.Services
{
    public class HashService : IHashService
    {
        private const string QUEUE_NAME = "hashes";

        private const int NUMBER_OF_HASHES = 40000;
        private const int HASH_CHUNK_SIZE = 1000;
        private const int HASH_BYTE_SIZE = 8;

        private const int WORKER_THREADS = 4;

        private readonly string _connectionString; //dbconnection string
        private readonly Random _rnd;


        private readonly ConnectionFactory _connectionFactory;

        public HashService(IConfiguration cfg)
        {
            var rMQSection = cfg.GetSection("rabbitmq");
            var hostName = rMQSection.GetValue<string>("host");
            var port = rMQSection.GetValue<int>("port");
            var userName = rMQSection.GetValue<string>("username");
            var password = rMQSection.GetValue<string>("password");
            _connectionString = cfg.GetConnectionString("DefaultConnection");


            _connectionFactory = new ConnectionFactory()
            {
                HostName = hostName,
                Port = port,
                UserName = userName,
                Password = password
            };

            _rnd = new Random();
        }


        public void GenerateAndSendHashesAsync(CancellationToken token)
        {
            var hashes = GenerateHashes();

            var options = new ParallelOptions() { CancellationToken = token, MaxDegreeOfParallelism = WORKER_THREADS };

            var chunks = hashes.Chunk(HASH_CHUNK_SIZE);

            Parallel.ForEach(chunks, options, (chunk, token) =>
            {
                PublishHashes(chunk);
            });
        }

        private List<string> GenerateHashes()
        {
            var hashes = new List<string>();
            using (var sha1 = SHA1.Create())
            {
                for (int i = 0; i < NUMBER_OF_HASHES; i++)
                {
                    var bytes = new byte[HASH_BYTE_SIZE];
                    _rnd.NextBytes(bytes);
                    var hashBytes = sha1.ComputeHash(bytes);
                    var hash = BitConverter.ToString(hashBytes).Replace("-", "");

                    hashes.Add(hash);
                }
            }

            return hashes;
        }

        private void PublishHashes(string[] hashesToSend)
        {
            var args = new Dictionary<string, object>()
                        {
                            { "x-queue-type", typeof(HashMessage).FullName }
                        };

            using (var connection = _connectionFactory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: QUEUE_NAME,
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: args
                );

                foreach (var hash in hashesToSend)
                {
                    var msgBody = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new HashMessage(hash)));
                    channel.BasicPublish(exchange: "",
                    routingKey: QUEUE_NAME,
                    basicProperties: null,
                    body: msgBody);
                }
            }
        }


        public async Task<List<HashStatistic>> GetHashStatisticsAsync()
        {
            var sql = @"
                    SELECT CAST([Date] AS DATE) AS Date, COUNT(*) AS Count
                    FROM Hashes
                    GROUP BY CAST([Date] AS DATE)
                ";

            using (var connection = new SqlConnection(_connectionString))
            {
                var groupedHashes = connection.Query(sql)
                    .Select(row => new HashStatistic { Date = (DateTimeOffset)row.Date, Count = (int)row.Count })
                    .ToList();

                return groupedHashes;
            }
        }
    }
}
