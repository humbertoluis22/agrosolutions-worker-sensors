using System;
using System.Collections.Generic;
using System.Text;

namespace AgrosolutionsWorkerSensors.Application.Interfaces
{
    public interface ISensorCache
    {
        Task<bool> IsActiveAsync(Guid sensorId);
        Task UpdateAsync(Guid sensorId, bool active);
    }
}
