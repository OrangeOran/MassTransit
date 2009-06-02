// Copyright 2007-2008 The Apache Software Foundation.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.RuntimeServices
{
	using System.IO;
	using FluentNHibernate.Cfg;
	using Infrastructure.Saga;
	using Model;
	using NHibernate;
	using NHibernate.Tool.hbm2ddl;
	using Saga;
	using Services.HealthMonitoring.Configuration;
	using Services.Timeout;
	using StructureMap;
	using StructureMap.Attributes;
	using StructureMapIntegration;
	using Transports;
	using Transports.Msmq;

	public class TimeoutServiceRegistry :
		MassTransitRegistryBase
	{
		private readonly IContainer _container;

		public TimeoutServiceRegistry(IContainer container)
			: base(typeof (MsmqEndpoint), typeof (LoopbackEndpoint))
		{
			_container = container;

			var configuration = container.GetInstance<IConfiguration>();

			ForRequestedType<ISessionFactory>()
				.CacheBy(InstanceScope.Singleton)
				.TheDefault.Is.ConstructedBy(context => CreateSessionFactory());

			ForRequestedType(typeof (ISagaRepository<>))
				.AddConcreteType(typeof (NHibernateSagaRepositoryForContainers<>));

			RegisterControlBus(configuration.TimeoutServiceControlUri, x => { x.SetConcurrentConsumerLimit(1); });

			RegisterServiceBus(configuration.TimeoutServiceDataUri, x =>
				{
					x.UseControlBus(_container.GetInstance<IControlBus>());

					ConfigureSubscriptionClient(configuration.SubscriptionServiceUri, x);

					x.ConfigureService<HealthClientConfigurator>(health => health.SetHeartbeatInterval(10));
				});
		}

		private static ISessionFactory CreateSessionFactory()
		{
			return Fluently.Configure()
				.Mappings(m => { m.FluentMappings.Add<TimeoutSagaMap>(); })
				.ExposeConfiguration(BuildSchema)
				.BuildSessionFactory();
		}

		private static void BuildSchema(NHibernate.Cfg.Configuration config)
		{
			var schemaFile = Path.Combine(Path.GetDirectoryName(typeof (TimeoutService).Assembly.Location), typeof (TimeoutService).Name + ".sql");

			new SchemaExport(config).SetOutputFile(schemaFile).Execute(false, false, false, true);
		}
	}
}