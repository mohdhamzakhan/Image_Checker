using System.Drawing;
using TorchSharp;

namespace Image_Checker.Services
{
    public class CheckCuda
    {
        private readonly Action<string>? _log;
        public CheckCuda(Action<string>? log = null)
        {
            _log = log;
        }
        public void CheckCudaForSystem()
        {

            try
            {
                bool available = torch.cuda.is_available();

                Log(
                    $"TorchSharp CUDA available: {available}");

                if (!available)
                {
                    Log(
                        "GPU training cannot start. Check the installed TorchSharp runtime packages.");

                    return;
                }

                Log("CUDA is available.");
            }
            catch (Exception ex)
            {
                Log($"CUDA initialization failed: {ex}");
            }
        }
        private void Log(string msg) => _log?.Invoke(msg);
    }
}
