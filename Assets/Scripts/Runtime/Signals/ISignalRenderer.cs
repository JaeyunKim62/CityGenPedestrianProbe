namespace CityGenPedestrianProbe
{
    public interface ISignalRenderer
    {
        void Render(in SignalIntent intent);

        void Release(string signalId);

        void StopAll();
    }
}
