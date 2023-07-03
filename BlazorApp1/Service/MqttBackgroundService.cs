using BlazorApp1.Service.IService;
using MQTTnet;
using MQTTnet.Client;
using System.Text.Json;
using Models;
using Business.Repository.IRepository;
using Yolov8Net;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing.Processing;
using DataAccess;
using System.Threading;
using System.Text.RegularExpressions;

namespace BlazorApp1.Service
{
	public class MqttBackgroundService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private IMqttBrokerService? _mqttBrokerService;
		private IEventRepository? _eventRepositroy;
		private IBoundingBoxRepository? _boundingBoxRepository;

		private static readonly Font font = new FontCollection().Add(@"/home/shyoun/Desktop/BlazorApp1/BlazorApp1/wwwroot/CONSOLA.TTF").CreateFont(11, FontStyle.Bold);


		public MqttBackgroundService(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			try
			{
				using (var scope = _serviceProvider.CreateScope())
				{
					_mqttBrokerService = scope.ServiceProvider.GetRequiredService<IMqttBrokerService>();

					// 연결
					var factory = new MqttFactory();
					var client = factory.CreateMqttClient();
					await _mqttBrokerService.ConnectAsync(client, "ictrobot.hknu.ac.kr", 8085, "mqtt-background-service");

					Func<MqttApplicationMessageReceivedEventArgs, Task> query = async e =>
					{
						using (var scope = _serviceProvider.CreateScope())
						{
							_eventRepositroy = scope.ServiceProvider.GetRequiredService<IEventRepository>();
							_boundingBoxRepository = scope.ServiceProvider.GetRequiredService<IBoundingBoxRepository>();

							var payload = e.ApplicationMessage.Payload;
							if (payload == null)
							{
								throw new ArgumentNullException(nameof(payload));
							}

							if (e.ApplicationMessage.Topic == "query")
							{
								var query = JsonSerializer.Deserialize<Query>(payload);
								if (query == null)
								{
									throw new ArgumentNullException(nameof(query));
								}

								if (string.IsNullOrEmpty(query.UserId))
								{
									throw new ArgumentNullException(nameof(query.UserId));
								}
								if (string.IsNullOrEmpty(query.Date))
								{
									throw new ArgumentNullException(nameof(query.Date));
								}

								if (string.IsNullOrEmpty(query.Image))
								{
									throw new ArgumentNullException(nameof(query.Image));
								}
								var image = Convert.FromBase64String(query.Image.Replace("data:image/jpeg;base64,", string.Empty));

								var root = @"/home/shyoun/Desktop/BlazorApp1/BlazorApp1/wwwroot";

								string? path;
								using (var stream = new MemoryStream(image))
								{
									path = Path.Combine(root, "queries", $"{query.UserId}_{query.Date}.jpeg");
									using (var fileStream = new FileStream(path, FileMode.Create))
									{
										await stream.CopyToAsync(fileStream);
									}
								}

								using var model = YoloV8Predictor.Create(@"/home/shyoun/Desktop/BlazorApp1/BlazorApp1/wwwroot/models/yolov8l.onnx");
								using var input = Image.Load(path);
								if (model == null)
								{
									throw new ArgumentNullException(nameof(model));
								}
								if (input == null)
								{
									throw new ArgumentNullException(nameof(input));
								}
								var predictions = model.Predict(input);
								if (!predictions.Any())
								{
									await Task.CompletedTask;
								}

								var boundingBoxes = new List<BoundingBoxDTO>();
								foreach (var prediction in predictions)
								{
									var originalImageHeight = input.Height;
									var originalImageWidth = input.Width;
									var x = (int)Math.Max(prediction.Rectangle.X, 0);
									var y = (int)Math.Max(prediction.Rectangle.Y, 0);
									var width = (int)Math.Min(originalImageWidth - x, prediction.Rectangle.Width);
									var height = (int)Math.Min(originalImageHeight - y, prediction.Rectangle.Height);
									var text = $"{prediction.Label.Name}: {prediction.Score}";
									var size = TextMeasurer.Measure(text, new TextOptions(font));
									input.Mutate(d => d.Draw(Pens.Solid(Color.Yellow, 2), new Rectangle(x, y, width, height)));
									input.Mutate(d => d.DrawText(new TextOptions(font) { Origin = new Point(x, (int)(y - size.Height - 1)) }, text, Color.Yellow));

									var boundingBox = new BoundingBoxDTO
									{
										X1 = x,
										Y1 = y,
										Width = width,
										Height = height,
										Label = prediction.Label.Name,
										Probability = prediction.Score
									};
									boundingBoxes.Add(boundingBox);
								}
								var imagePath = Path.Combine(root, "events", $"{query.UserId}_{query.Date}.jpeg");
								input.Save(imagePath);

								imagePath = imagePath.Substring(root.Length + 1);
								var eventDTO = new EventDTO
								{
									UserId = query.UserId,
									Date = query.Date,
									ImagePath = imagePath
								};
								var createdEventDTO = await _eventRepositroy.Create(eventDTO);

								foreach (var boundingBox in boundingBoxes)
								{
									boundingBox.EventId = createdEventDTO.Id;
								}

								var count = await _boundingBoxRepository.Create(boundingBoxes);
								if (count <= 0)
								{
									throw new Exception($"BoundingBox를 저장하는 데 실패했습니다.");
								}

								if (File.Exists(path))
								{
									File.Delete(path);
								}
							}
						}
						await Task.CompletedTask;
					};
					_mqttBrokerService.RegisterHandler(client, query);

					var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
						.WithTopicFilter(f =>
						{
							f.WithTopic("query");
						}).Build();
					await client.SubscribeAsync(subscribeOptions);
				}

				while (!stoppingToken.IsCancellationRequested)
				{
					await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
				}

				await Task.CompletedTask;

			}
			catch (Exception ex)
			{
				Console.WriteLine($"{ex.GetType().FullName}: {ex.Message}");
			}
		}
	}
}
