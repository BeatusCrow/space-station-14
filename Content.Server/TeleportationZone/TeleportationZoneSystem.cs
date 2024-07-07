using Content.Server.AlertLevel;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Coordinates;
using Content.Shared.TeleportationZone;
using Content.Shared.Maps;
using Content.Server.NukeOps;
using Content.Shared.Weather;
using Robust.Server.GameObjects;
using Robust.Shared.Asynchronous;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.TeleportationZone
{
    public sealed class TeleportationZoneSystem : EntitySystem
    {
        [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
        [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly EntityLookupSystem _lookupSystem = default!;
        [Dependency] private readonly IEntityManager _entMan = default!;
        [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
        [Dependency] private readonly SharedMapSystem _mapSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly StationSystem _station = default!;
        [Dependency] private readonly UserInterfaceSystem _userInterface = default!;

        public override void Initialize()
        {
            base.Initialize();
            // UI
            SubscribeLocalEvent<TeleportationZoneConsoleComponent, BoundUIOpenedEvent>(OnBoundUIOpened);
            SubscribeLocalEvent<TeleportationZoneConsoleComponent, TeleportationZoneRefreshMessage>(OnRefreshLandingButtonPressed);
            SubscribeLocalEvent<TeleportationZoneConsoleComponent, TeleportationZoneStartMessage>(OnStartLandingButtonPressed);
            SubscribeLocalEvent<TeleportationZoneConsoleComponent, TeleportationZonePointSelectedMessage>(OnPointSelected);
            // NukeConsole
            SubscribeLocalEvent<WarDeclaredEvent>(OnWarDeclared);
        }


        #region Ui
        private void OnPointSelected(EntityUid uid, TeleportationZoneConsoleComponent component, TeleportationZonePointSelectedMessage args)
        {
            component.LandingPointId = args.Point;

            UpdateUI(uid, true);
        }

        private void OnBoundUIOpened(EntityUid uid, TeleportationZoneConsoleComponent component, BoundUIOpenedEvent args)
        {
            UpdateUI(uid, false);
        }


        private void OnRefreshLandingButtonPressed(EntityUid uid, TeleportationZoneConsoleComponent component, TeleportationZoneRefreshMessage message)
        {
            UpdateUI(uid, false);
        }

        private void UpdateUI(EntityUid uid, bool canActivate)
        {
            Dictionary<int, string> points = new Dictionary<int, string>(); // A dictionary is needed to convey the names of points
            var query = AllEntityQuery<LandingPointComponent>();
            while (query.MoveNext(out var item_uid, out var item_comp))
            {
                if (!TryComp<MetaDataComponent>(item_uid, out var item_MetaData))
                    return;

                var NameLandingPoint = "unknow";

                if (item_MetaData.EntityPrototype!.EditorSuffix != null)
                    NameLandingPoint = item_MetaData.EntityPrototype.EditorSuffix;

                points.Add(item_uid.Id, NameLandingPoint);
            }

            var state = new TeleportationZoneUiState(true, canActivate, points);
            _userInterface.SetUiState(uid, TeleportationZoneUiKey.Key, state); // Updating the user interface
        }

        private void OnStartLandingButtonPressed(EntityUid uid, TeleportationZoneConsoleComponent component, TeleportationZoneStartMessage message)
        {
            EntityUid end_station_uid = uid;
            EntityUid end_stationAlert_uid = uid;
            EntityUid start_station_uid = uid;
            var x_form = Transform(uid);
            var Coords = x_form.Coordinates;
            int count = 0;

            var query_point = AllEntityQuery<LandingPointComponent>();
            while (query_point.MoveNext(out var item_uid, out var item_comp))
            {
                if (item_uid.Id == component.LandingPointId)
                {
                    x_form = Transform(item_uid);
                    Coords = x_form.Coordinates;
                    var query_station = AllEntityQuery<BecomesStationComponent>();
                    while (query_station.MoveNext(out var st_uid, out var data))
                    {
                        if (Transform(st_uid).MapID == x_form.MapID)
                        {
                            end_station_uid = st_uid;
                            break;
                        }
                    }

                    var query_stationAlert = EntityQueryEnumerator<StationDataComponent>();
                    while (query_stationAlert.MoveNext(out var stA_uid, out var dataA))
                    {
                        foreach (var gridUid in dataA.Grids)
                        {
                            if (Transform(gridUid).MapID == x_form.MapID)
                            {
                                end_stationAlert_uid = stA_uid;
                                break;
                            }
                        }
                    }
                    break;
                }
            }

            var query_lighthouse = AllEntityQuery<TeleportationZoneBeaconComponent>();
            while (query_lighthouse.MoveNext(out var item_uid, out var item_comp))
            {
                x_form = Transform(item_uid);
                var landinglighthousCoords = x_form.Coordinates;
                if (count == 0) // We assign the values of the first "beacon"
                {
                    component.top_border = landinglighthousCoords.Y;
                    component.bottom_border = landinglighthousCoords.Y;
                    component.left_border = landinglighthousCoords.X;
                    component.right_border = landinglighthousCoords.X;
                    count++;
                    continue;
                }

                if (landinglighthousCoords.Y < component.bottom_border)
                    component.bottom_border = landinglighthousCoords.Y;
                if (landinglighthousCoords.Y > component.top_border)
                    component.top_border = landinglighthousCoords.Y;
                if (landinglighthousCoords.X > component.right_border)
                    component.right_border = landinglighthousCoords.X;
                if (landinglighthousCoords.X < component.left_border)
                    component.left_border = landinglighthousCoords.X;

                var query_station = AllEntityQuery<BecomesStationComponent>();
                while (query_station.MoveNext(out var st_uid, out var data))
                {
                    if (Transform(st_uid).MapID == x_form.MapID)
                    {
                        start_station_uid = st_uid;
                        break;
                    }
                }
            }

            if (!TryComp<MapGridComponent>(end_station_uid, out var end_station_gridComp))
                return;

            var xform = Transform(end_station_uid);
            Random rnd = new Random();
            while (true)
            {
                int radius = rnd.Next(-5, 6);
                var x_coord = (int) (Coords.X - 0.5f) + radius; // This way we create a spread on landing to make it more interesting (spread within 5 tiles).
                radius = rnd.Next(-5, 6);
                var y_coord = (int) (Coords.Y - 0.5f) + radius;

                var tile = new Vector2i(x_coord, y_coord);
                if (_atmosphere.IsTileSpace(end_station_uid, xform.MapUid, tile)) // the point from which the drop pod generation starts should not be a space tile...
                {
                    continue; // if this is the case, then we are looking for the next possible point
                }

                var pos = _mapSystem.GridTileToLocal(end_station_uid, end_station_gridComp, tile);
                if (!TryComp<MapGridComponent>(start_station_uid, out var start_station_gridComp))
                    return;

                if(TryComp<NukeTeleportationZoneComponent>(uid, out var nukeComp))
                {
                    _alertLevelSystem.SetLevel(end_stationAlert_uid, "red", true, true, true);
                    _chat.DispatchGlobalAnnouncement(string.Format(nukeComp.Text, nukeComp.Time, Coords.X, Coords.Y), "Central Command", true, nukeComp.Sound, nukeComp.Color);
                    Task.Factory.StartNew(() => Thread.Sleep(nukeComp.Time! * 1000))
                                         .ContinueWith((t) =>
                        {
                            CheckTils(component, start_station_uid, start_station_gridComp, end_station_uid, end_station_gridComp, pos);
                        }, TaskScheduler.FromCurrentSynchronizationContext());
                    break;
                }
                CheckTils(component, start_station_uid, start_station_gridComp, end_station_uid, end_station_gridComp, pos);
                break;
            }
        }
        #endregion

        #region moving the capsule
        /// <summary>
        /// This method iterates through all tiles within our borders (which are set by beacons) and creates similar ones (objects are transferred, not created) at the point of grounding
        /// </summary>
        /// <param name="start_station_uid"> it is needed to identify an object that is located in a certain tile at the Nuke Ops station </param>
        /// <param name="start_grid"> it is needed to determine the type of tile at the Nuke Ops station </param>
        /// <param name="end_station_uid"> it is necessary to understand where to move objects </param>
        /// <param name="end_grid"> it is needed to determine the local coordinates at the station where the disembarkation will take place </param>
        /// <param name="pos"> it is necessary to move the object to the desired coordinates </param>
        private void CheckTils(TeleportationZoneConsoleComponent component, EntityUid start_station_uid, MapGridComponent start_grid, EntityUid end_station_uid, MapGridComponent end_grid, EntityCoordinates pos)
        {
            component.left_border -= 0.5f;  // we are aligning the values of the borders, because now we are counting
            component.right_border -= 0.5f; // from the center of the tile (on which the lighthouse stands), and not from the lower-left corner
            component.top_border -= 0.5f;
            component.bottom_border -= 0.5f;

            for (int i = (int) component.left_border + 1; i < (int) component.right_border; i++)
            {
                for (int j = (int) component.top_border - 1; j > (int) component.bottom_border; j--)
                {
                    var coords = new Vector2i(i, j);

                    if (!start_grid.TryGetTileRef(coords, out var tileRef))
                        continue;

                    int dX = i - ((int) component.left_border + 1);
                    int dY = j - ((int) component.top_border - 1);
                    var TileType = tileRef.Tile.GetContentTileDefinition().ID; // defining the tile type
                    if (TileType == "Plating") // if it's just "plating", then the objects from this tile are not portable (they are outside the TeleportationZone)
                    {
                        foreach (var entity in _lookupSystem.GetLocalEntitiesIntersecting(start_station_uid, coords))
                        {
                            if (TryComp<BlockWeatherComponent>(entity, out var BlockWeatherComp)) // We check for the presence of a wall above a tile of this type
                            {
                                CreateShuttleFloorUnderWall(end_station_uid, end_grid, pos, dX, dY);
                                if (_entMan.TryGetComponent(entity, out TransformComponent? transComp))
                                {
                                    if (transComp.Anchored)
                                    {
                                        CreateEntityOnShuttle(end_station_uid, end_grid, pos, dX, dY, entity, transComp, true, transComp.LocalRotation);
                                        continue;
                                    }
                                    CreateEntityOnShuttle(end_station_uid, end_grid, pos, dX, dY, entity, transComp, false, transComp.LocalRotation);
                                }
                            }
                        }
                        continue;
                    }

                    CreateShuttleFloor(end_station_uid, end_grid, pos, dX, dY, (string) TileType);
                    foreach (var entity in _lookupSystem.GetLocalEntitiesIntersecting(start_station_uid, coords))
                    {
                        if (_entMan.TryGetComponent(entity, out TransformComponent? transComp))
                        {
                            var parent = transComp.ParentUid;

                            if (parent == start_station_uid) // // this is necessary to teleport a man in a suit, not in parts :)
                            {
                                if (transComp.Anchored)
                                {
                                    CreateEntityOnShuttle(end_station_uid, end_grid, pos, dX, dY, entity, transComp, true, transComp.LocalRotation);
                                    continue;
                                }
                                CreateEntityOnShuttle(end_station_uid, end_grid, pos, dX, dY, entity, transComp, false, transComp.LocalRotation);
                            }
                        }
                    }
                    CreatePlatingFloor(start_station_uid, start_grid, i, j);
                }
            }
        }

        private void CreatePlatingFloor(EntityUid start_station_uid, MapGridComponent start_gridComp, int X, int Y)
        {
            var tile = new Vector2i(X, Y);
            var new_pos = _mapSystem.GridTileToLocal(start_station_uid, start_gridComp, tile);
            var plating = _tileDefinitionManager["Plating"];
            start_gridComp.SetTile(new_pos, new Tile(plating.TileId));
        }

        /// <summary>
        /// This method is needed to create a floor at the point of movement that corresponds to what is located at the Nuke Ops station
        /// </summary>
        private void CreateShuttleFloor(EntityUid end_station_uid, MapGridComponent end_gridComp, EntityCoordinates pos, int dX, int dY, string TileType)
        {
            int coordX = (int) (pos.X - 0.5f) + dX;
            int coordY = (int) (pos.Y - 0.5f) + dY;
            var tile = new Vector2i(coordX, coordY);
            foreach (var entity in _lookupSystem.GetLocalEntitiesIntersecting(end_station_uid, tile))
            {

                if (TryComp<LandingPointComponent>(entity, out var LandComp))
                    continue;

                Del(entity); // iterate over the objects that are at the desired coordinates and delete them
            }
            var new_pos = _mapSystem.GridTileToLocal(end_station_uid, end_gridComp, tile);
            var plating = _tileDefinitionManager[TileType];
            end_gridComp.SetTile(new_pos, new Tile(plating.TileId));
        }


        /// <summary>
        /// This method is needed to create a floor under the walls.
        /// If part of the shuttle ends up in space, then this method will create a beautiful and logical grid,
        /// rather than a steel floor under the wall (this is important when using corner walls)
        /// </summary>
        private void CreateShuttleFloorUnderWall(EntityUid end_station_uid, MapGridComponent end_gridComp, EntityCoordinates pos, int dX, int dY)
        {
            int coordX = (int) (pos.X - 0.5f) + dX;
            int coordY = (int) (pos.Y - 0.5f) + dY;
            var tile = new Vector2i(coordX, coordY);
            var xform = Transform(end_station_uid);
            var new_pos = _mapSystem.GridTileToLocal(end_station_uid, end_gridComp, tile);
            var plating = _tileDefinitionManager["Lattice"];

            if (_atmosphere.IsTileSpace(end_station_uid, xform.MapUid, tile))
            {
                end_gridComp.SetTile(new_pos, new Tile(plating.TileId));
            }

            foreach (var entity in _lookupSystem.GetLocalEntitiesIntersecting(end_station_uid, tile)) // iterate over the objects that are at the desired coordinates and delete them
            {
                if (TryComp<LandingPointComponent>(entity, out var LandComp))
                    continue;

                Del(entity);
            }
        }


        /// <summary>
        /// This method is needed to move objects from one station to another when the TeleportationZone is activated.
        /// We just change the coordinates of the objects and save some of their components.
        /// </summary>
        private void CreateEntityOnShuttle(EntityUid end_station_uid, MapGridComponent end_gridComp, EntityCoordinates pos, int dX, int dY, EntityUid entity, TransformComponent transComp, bool isAnch, Angle rot)
        {
            int coordX = (int) (pos.X - 0.5f) + dX;
            int coordY = (int) (pos.Y - 0.5f) + dY;
            var tile = new Vector2i(coordX, coordY);
            var new_pos = _mapSystem.GridTileToLocal(end_station_uid, end_gridComp, tile);
            _transform.SetCoordinates(entity, new_pos);
            transComp.Anchored = isAnch; // objects (e.g. walls) lose their attachment to the floor during teleportation
            transComp.LocalRotation = rot; // this is necessary so that the object is correctly rotated relative to the new station 
        }
        #endregion

        #region NukeConsole
        private void OnWarDeclared(ref WarDeclaredEvent ev)
        {
            var query = AllEntityQuery<NukeTeleportationZoneComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                comp.WarDeclared = true;
            }
        }
        #endregion
    }
}
