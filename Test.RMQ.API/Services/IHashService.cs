using Test.RMQ.Common;

namespace Test.RMQ.API.Services
{
    public interface IHashService
    {
        void GenerateAndSendHashesAsync(CancellationToken token);

        Task<List<HashStatistic>> GetHashStatisticsAsync();
    }
}
