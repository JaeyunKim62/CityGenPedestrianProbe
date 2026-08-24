using System;

namespace CityGenPedestrianProbe
{
    public interface ISignalRenderer
    {
        event Action<string> SignalUnresolved;

        void Render(in SignalIntent intent);

        void Release(string signalId);

        void StopAll();
    }
}
