using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace HyperWindowsService
{
    public class HyperClient
    {
        private const string PIPE_NAME = "hyper";
        private NamedPipeClientStream _pipeClient;
        private readonly ILogger<HyperClient> _logger;
        public bool IsConnected => _pipeClient.IsConnected;

        private TaskCompletionSource<bool> _serverDisconnectedTask = new TaskCompletionSource<bool>();
        public HyperClient(ILogger<HyperClient> logger)
        {
            _logger = logger;
            _pipeClient = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut, PipeOptions.Asynchronous);
        }
        public bool EnsureConnection()
        {
            if (!_pipeClient.IsConnected && !_serverDisconnectedTask.Task.IsCompleted)
                _serverDisconnectedTask.SetResult(true);
            return _pipeClient.IsConnected;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _pipeClient.ConnectAsync(cancellationToken);
                _serverDisconnectedTask = new TaskCompletionSource<bool>();
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }
        }

        public  Task WaitForDisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)  return Task.FromResult(true);
            else
            {
                using (cancellationToken.Register(() => _serverDisconnectedTask.TrySetCanceled()))
                {
                    return _serverDisconnectedTask.Task;
                }
            }
        }
        public void Close() => _pipeClient.Close();

        /// <summary>
        /// Sends a message to the hyper server in json format
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public  Task SendMessageAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        {
            if (!EnsureConnection())
                return Task.FromResult(true);
            var jsonMessage = JsonSerializer.Serialize(message, new JsonSerializerOptions());
            return SendMessageAsync(jsonMessage, cancellationToken);
        }

        /// <summary>
        /// Sends a message to the hyper server as an ASCII string
        /// </summary>
        /// <param name="message"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task  SendMessageAsync(string message, CancellationToken cancellationToken = default)
        {

            try
            {
                var messageBytes = Encoding.ASCII.GetBytes(message);
                await _pipeClient.WriteAsync(messageBytes, cancellationToken);
                // Wait for server to read message
                await Task.Run(() => _pipeClient.WaitForPipeDrain());
            }
            // Catch the IOException that is raised if the pipe is broken
            // or disconnected.
            catch (IOException e)
            {
                _logger.LogWarning(e.Message);
                EnsureConnection();
            }
        }
    }
}
