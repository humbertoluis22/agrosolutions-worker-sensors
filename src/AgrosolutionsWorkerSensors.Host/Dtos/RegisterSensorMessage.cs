using AgrosolutionsWorkerSensors.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgrosolutionsWorkerSensors.Registration.Dtos;
public record RegisterSensorMessage(
    Guid FieldId,
    Guid SensorId,
    DateTime DtCreated,
    SensorType TypeSensor,
    bool StatusSensor,
    TypeOperation TypeOperation
);