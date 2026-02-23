using AgrosolutionsWorkerSensors.Application.Interfaces;
using AgrosolutionsWorkerSensors.Domain.Entities;

namespace AgrosolutionsWorkerSensors.Application.UseCases
{
    public class ProcessRawSensorDataUseCase(ISensorCache cache, ISensorRawRepository repository)
    {
        private readonly ISensorCache _cache = cache;
        private readonly ISensorRawRepository _repository = repository;

        public async Task ExecuteAsync(SensorRaw raw)
        {
            var isActive = await _cache.IsActiveAsync(raw.SensorId);

            if (!isActive)
                return;

            await _repository.SaveAsync(raw);
        }
    }
}
