using AgrosolutionsWorkerSensors.Application.Interfaces;
using AgrosolutionsWorkerSensors.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgrosolutionsWorkerSensors.Application.UseCases
{
    public class ProcessRawSensorDataUseCase
    {
        private readonly ISensorCache _cache;
        private readonly ISensorRawRepository _repository;

        public ProcessRawSensorDataUseCase(
            ISensorCache cache,
            ISensorRawRepository repository)
        {
            _cache = cache;
            _repository = repository;
        }

        public async Task ExecuteAsync(SensorRaw raw)
        {
            var isActive = await _cache.IsActiveAsync(raw.SensorId);

            if (!isActive)
                return;

            await _repository.SaveAsync(raw);
        }
    }
}
