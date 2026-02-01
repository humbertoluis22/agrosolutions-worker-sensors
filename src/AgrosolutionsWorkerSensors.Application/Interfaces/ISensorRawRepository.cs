using AgrosolutionsWorkerSensors.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgrosolutionsWorkerSensors.Application.Interfaces
{
    public interface ISensorRawRepository
    {
        Task SaveAsync(SensorRaw raw);
    }
}
