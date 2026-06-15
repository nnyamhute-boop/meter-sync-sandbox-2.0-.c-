using System.Threading.Tasks;
using MeterSync.Core.Models;

namespace MeterSync.Core.Interfaces
{
    public interface IMeterOrchestrator
    {
        Task ProcessAsync(IncomingReading reading);
    }
}
