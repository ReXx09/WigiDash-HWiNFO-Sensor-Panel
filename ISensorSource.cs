using System;

namespace HwinfoSensorPanel;

public interface ISensorSource : IDisposable
{
    SensorSnapshot Read();
}