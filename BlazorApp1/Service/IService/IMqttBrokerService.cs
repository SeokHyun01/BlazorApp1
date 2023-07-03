using MQTTnet.Client;

namespace BlazorApp1.Service.IService
{
	public interface IMqttBrokerService
	{
		ValueTask ConnectAsync(IMqttClient client, string host, int port, string id);
		void RegisterHandler(IMqttClient client, Func<MqttApplicationMessageReceivedEventArgs, Task> handler);
	}
}
