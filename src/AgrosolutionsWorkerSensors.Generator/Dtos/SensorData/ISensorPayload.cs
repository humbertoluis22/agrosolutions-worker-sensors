using System;
using System.Collections.Generic;
using System.Text;

namespace AgrosolutionsWorkerSensors.Generator.Dtos.SensorData;
public interface ISensorPayload { }

public record SoloData(double Umidade, double Ph, double Nitrogenio, double Fosforo, double Potassio) : ISensorPayload;

public record SiloData(double NivelPreenchimento, double TemperaturaMedia, double Co2) : ISensorPayload;

public record MeteorologicaData(double Temperatura, double Umidade, double VelocidadeVento, string DirecaoVento, double ChuvaUltimaHora, double PontoOrvalho) : ISensorPayload;