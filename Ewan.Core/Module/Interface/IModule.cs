namespace Ewan.Core.Module.Interface
{
    public interface IModule
    {
        void Init();

        bool Run();

        void SetObject(object obj);

        void Destroy();
    }
}
