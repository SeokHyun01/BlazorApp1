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
using DataAccess.Migrations;

namespace BlazorApp1.Service
{
	public class MqttBackgroundService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private IMqttBrokerService? _mqttBrokerService;
		private IEventRepository? _eventRepositroy;
		private IBoundingBoxRepository? _boundingBoxRepository;

		private static readonly string ROOT = @"/home/shyoun/Desktop/BlazorApp1/BlazorApp1/wwwroot";
		private static readonly Font FONT = new FontCollection().Add(@"/home/shyoun/Desktop/BlazorApp1/BlazorApp1/wwwroot/CONSOLA.TTF").CreateFont(11, FontStyle.Bold);


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
						var currentTime = DateTime.Now;
						var topic = e.ApplicationMessage.Topic;
						Console.WriteLine($"{topic}에서 {currentTime}에 메시지를 수신했습니다.");

						using (var scope = _serviceProvider.CreateScope())
						{
							_eventRepositroy = scope.ServiceProvider.GetRequiredService<IEventRepository>();
							_boundingBoxRepository = scope.ServiceProvider.GetRequiredService<IBoundingBoxRepository>();

							var payload = e.ApplicationMessage.Payload;
							if (payload == null)
							{
								throw new ArgumentNullException(nameof(payload));
							}

							if (e.ApplicationMessage.Topic == "event")
							{
								var skipQuery = JsonSerializer.Deserialize<Query>(payload);
								if (skipQuery == null)
								{
									throw new ArgumentNullException(nameof(skipQuery));
								}

								if (string.IsNullOrEmpty(skipQuery.Image))
								{
									throw new ArgumentNullException(nameof(skipQuery.Image));
								}
								var image = Convert.FromBase64String(skipQuery.Image.Replace("data:image/jpeg;base64,", string.Empty));

								string? path;
								using (var stream = new MemoryStream(image))
								{
									path = Path.Combine(ROOT, "events", $"{skipQuery.UserId}_{skipQuery.Date}.jpeg");
									using (var fileStream = new FileStream(path, FileMode.Create))
									{
										await stream.CopyToAsync(fileStream);
									}
								}
								path = path.Substring(ROOT.Length + 1);

								var eventDTO = new EventDTO
								{
									UserId = skipQuery.UserId,
									Date = skipQuery.Date,
									ImagePath = path
								};
								var createdEventDTO = await _eventRepositroy.Create(eventDTO);

								var boundingBoxes = new List<BoundingBoxDTO>();
								foreach (var boundingBox in skipQuery.BoundingBoxes)
								{
									var bBox = new BoundingBoxDTO
									{
										EventId = createdEventDTO.Id,
										X1 = boundingBox.X1,
										Y1 = boundingBox.Y1,
										Width = boundingBox.X2 - boundingBox.X1,
										Height = boundingBox.Y2 - boundingBox.Y1,
										Label = boundingBox.Label,
										Probability = boundingBox.Probability
									};
									boundingBoxes.Add(bBox);
								}
								var count = await _boundingBoxRepository.Create(boundingBoxes);
								if (count <= 0)
								{
									throw new Exception($"BoundingBox를 저장하는 데 실패했습니다.");
								}
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
								
								string? queryImagePath;
								using (var stream = new MemoryStream(image))
								{
									queryImagePath = Path.Combine(ROOT, "queries", $"{query.UserId}_{query.Date}.jpeg");
									using (var fileStream = new FileStream(queryImagePath, FileMode.Create))
									{
										await stream.CopyToAsync(fileStream);
									}
								}

								using var model = YoloV8Predictor.Create(@"/home/shyoun/Desktop/BlazorApp1/BlazorApp1/wwwroot/models/yolov8l.onnx");
								using var input = Image.Load(queryImagePath);
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
									var size = TextMeasurer.Measure(text, new TextOptions(FONT));
									input.Mutate(d => d.Draw(Pens.Solid(Color.Yellow, 2), new Rectangle(x, y, width, height)));
									input.Mutate(d => d.DrawText(new TextOptions(FONT) { Origin = new Point(x, (int)(y - size.Height - 1)) }, text, Color.Yellow));
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

								var eventImagePath = Path.Combine(ROOT, "events", $"{query.UserId}_{query.Date}.jpeg");
								input.Save(eventImagePath);
								eventImagePath = eventImagePath.Substring(ROOT.Length + 1);
								var eventDTO = new EventDTO
								{
									UserId = query.UserId,
									Date = query.Date,
									ImagePath = eventImagePath
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

								if (File.Exists(queryImagePath))
								{
									File.Delete(queryImagePath);
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
