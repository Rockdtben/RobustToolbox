using System.Linq;
using Moq;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Server.Physics;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Physics.Broadphase;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Robust.UnitTesting.Server.Maps
{
    [TestFixture]
    public class MapLoaderTest : RobustUnitTest
    {
        private const string MapData = @"
meta:
  format: 2
  name: DemoStation
  author: Space-Wizards
  postmapinit: false
grids:
- settings:
    chunksize: 16
    tilesize: 1
    snapsize: 1
  chunks: []
tilemap: {}
entities:
- uid: 0
  components:
  - parent: null
    type: Transform
  - index: 0
    type: MapGrid
  - fixtures:
    - shape:
        !type:PhysShapeGrid
          grid: 0
    type: Physics
- uid: 1
  type: MapDeserializeTest
  components:
  - type: MapDeserializeTest
    foo: 3
  - parent: 0
    type: Transform
";

        private const string Prototype = @"
- type: entity
  id: MapDeserializeTest
  components:
  - type: MapDeserializeTest
    foo: 1
    bar: 2

";

        protected override void OverrideIoC()
        {
            base.OverrideIoC();
            var mockFormat = new Mock<ICustomFormatManager>();
            var mock = new Mock<IEntitySystemManager>();
            var broady = new BroadPhaseSystem();
            var physics = new PhysicsSystem();
            mock.Setup(m => m.GetEntitySystem<SharedBroadPhaseSystem>()).Returns(broady);
            mock.Setup(m => m.GetEntitySystem<SharedPhysicsSystem>()).Returns(physics);

            IoCManager.RegisterInstance<IEntitySystemManager>(mock.Object, true);
            IoCManager.RegisterInstance<ICustomFormatManager>(mockFormat.Object, true);
        }


        [OneTimeSetUp]
        public void Setup()
        {
            IoCManager.Resolve<IComponentFactory>().Register<MapDeserializeTestComponent>();

            IoCManager.Resolve<IComponentManager>().Initialize();

            var resourceManager = IoCManager.Resolve<IResourceManagerInternal>();
            resourceManager.Initialize(null);
            resourceManager.MountString("/TestMap.yml", MapData);
            resourceManager.MountString("/Prototypes/TestMapEntity.yml", Prototype);

            IoCManager.Resolve<IPrototypeManager>().LoadDirectory(new ResourcePath("/Prototypes"));

            var map = IoCManager.Resolve<IMapManager>();
            map.Initialize();
            map.Startup();
        }

        [Test]
        public void TestDataLoadPriority()
        {
            // TODO: Fix after serv3
            var map = IoCManager.Resolve<IMapManager>();

            var entMan = IoCManager.Resolve<IEntityManager>();

            var mapId = map.CreateMap();
            var mapLoad = IoCManager.Resolve<IMapLoader>();
            var grid = mapLoad.LoadBlueprint(mapId, "/TestMap.yml");

            Assert.That(grid, NUnit.Framework.Is.Not.Null);

            var entity = entMan.GetEntity(grid!.GridEntityId).Transform.Children.Single().Owner;
            var c = entity.GetComponent<MapDeserializeTestComponent>();

            Assert.That(c.Bar, Is.EqualTo(2));
            Assert.That(c.Foo, Is.EqualTo(3));
            Assert.That(c.Baz, Is.EqualTo(-1));
        }

        private sealed class MapDeserializeTestComponent : Component
        {
            public override string Name => "MapDeserializeTest";

            public int Foo { get; set; }
            public int Bar { get; set; }
            public int Baz { get; set; }

            public override void ExposeData(ObjectSerializer serializer)
            {
                base.ExposeData(serializer);

                serializer.DataField(this, p => p.Foo, "foo", -1);
                serializer.DataField(this, p => p.Bar, "bar", -1);
                serializer.DataField(this, p => p.Baz, "baz", -1);
            }
        }
    }
}
