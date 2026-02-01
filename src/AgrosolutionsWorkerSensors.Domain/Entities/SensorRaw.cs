using AgrosolutionsWorkerSensors.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgrosolutionsWorkerSensors.Domain.Entities
{
    public class SensorRaw
    {
        public Guid SensorId { get; set; } // Sensor_id
        public Guid FieldId { get; set; }
        public DateTime DtCreated { get; set; }
        public SensorType TypeSensor { get; set; }
        public bool StatusSensor {  get; set; }
        public TypeOperation TypeOperation { get; set; }
     
    }
}
