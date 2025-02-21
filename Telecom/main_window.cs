﻿using System;
using System.Collections.Generic;
using System.Linq;
using RealAntennas;
using UnityEngine;
using principia.ksp_plugin_adapter;

namespace σκοπός {

internal class MainWindow : principia.ksp_plugin_adapter.SupervisedWindowRenderer {
  public MainWindow(Telecom telecom) : base(telecom) {
    telecom_ = telecom;
  }

  public bool show_network { get; private set; } = false;
  private Vector2 scrollPosition;

  protected override string Title => "Σκοπός Telecom network overview";

  protected override void RenderWindowContents(int window_id) {
    //21 width because a horizontal scrollbar will show up with 20 width
    using (var scrollViewScope = new GUILayout.ScrollViewScope(scrollPosition, GUILayoutHeight(20), GUILayoutWidth(21))) {
      scrollPosition = scrollViewScope.scrollPosition;
      using (new UnityEngine.GUILayout.VerticalScope()) {
        show_network = UnityEngine.GUILayout.Toggle(show_network, "Show network");
        if (false) {
          using (new UnityEngine.GUILayout.HorizontalScope()) {
            if (UnityEngine.GUILayout.Button("Add nominal location") && FlightGlobals.ActiveVessel != null) {
              telecom_.network.AddNominalLocation(FlightGlobals.ActiveVessel);
              return;
            }
            if (UnityEngine.GUILayout.Button("Clear nominal locations")) {
              telecom_.network.ClearNominalLocations();
              return;
            }
            telecom_.network.freeze_customers_ =
                UnityEngine.GUILayout.Toggle(telecom_.network.freeze_customers_, "Freeze customers");
          }
          foreach (Vector3d location in telecom_.network.GetNominalLocationLatLonAlts()) {
            UnityEngine.GUILayout.Label($"{location.x:F2}°, {location.y:F2}°, {location.z / 1000:F0} km");
          }
          using (new UnityEngine.GUILayout.HorizontalScope()) {
            if (int.TryParse(UnityEngine.GUILayout.TextField(
                  telecom_.network.customer_pool_size.ToString()), out int pool_size)) {
              telecom_.network.customer_pool_size = Math.Max(pool_size, 0);
            }
          }
        }
        var inspected_connections = connection_inspectors_.Keys.ToArray();
        foreach (var inspected_connection in inspected_connections) {
          if (!telecom_.network.contracted_connections.Contains(inspected_connection)) {
            connection_inspectors_[inspected_connection].DisposeWindow();
            connection_inspectors_.Remove(inspected_connection);
          }
        }
        foreach (var contracted_connection in telecom_.network.contracted_connections) {
         if (!connection_inspectors_.ContainsKey(contracted_connection)) {
            connection_inspectors_.Add(
                contracted_connection,
                new ConnectionInspector(telecom_, contracted_connection));
         }
        }
        foreach (var contract in open_contracts_.Keys.ToArray()) {
          if (!telecom_.network.connections_by_contract.ContainsKey(contract)) {
            open_contracts_.Remove(contract);
          }
        }
        foreach (var contract in telecom_.network.connections_by_contract.Keys) {
          if (!open_contracts_.ContainsKey(contract)) {
            open_contracts_.Add(contract, false);
          }
        }
        var connectionLabelStyle = Style.Multiline(GUI.skin.label);
        var okStyle = new GUIStyle(Style.RightAligned(GUI.skin.label)){
          focused = {
              textColor = XKCDColors.Lime
          },
          normal = {
              textColor = XKCDColors.Lime
          }
        };
        var partialStyle = Style.RightAligned(Style.Warning(GUI.skin.label));
        var disconnectedStyle = Style.RightAligned(Style.Error(GUI.skin.label));
        
        foreach (var contract_connections in telecom_.network.connections_by_contract) {
          var contract = contract_connections.Key;
          var connections = contract_connections.Value;
          using (new UnityEngine.GUILayout.HorizontalScope()) {
            bool allUp = true;
            bool allDown = true;
            foreach (var connection in connections) {
              if (connection is PointToMultipointConnection point_to_multipoint) {
                var tx = telecom_.network.GetStation(point_to_multipoint.tx_name);
                if (point_to_multipoint.channel_services.Length == 1) {
                  var services = point_to_multipoint.channel_services[0];
                  bool available = services.basic.available;
                  if (available) {
                    allDown = false;
                  } else {
                    allUp = false;
                  }
                }
                for (int i = 0; i < point_to_multipoint.rx_names.Length; ++i) {
                  var services = point_to_multipoint.channel_services[i];
                  bool available = services.basic.available;
                  if (available) {
                    allDown = false;
                  } else {
                    allUp = false;
                  }
                }
              } else if (connection is DuplexConnection duplex) {
                bool available = duplex.basic_service.available;
                  if (available) {
                    allDown = false;
                  } else {
                    allUp = false;
                  }
              }
            }
            if (UnityEngine.GUILayout.Button(
                  open_contracts_[contract] ? "−" : "+", GUILayoutWidth(1))) {
              open_contracts_[contract] = !open_contracts_[contract];
              ScheduleShrink();
              return;
            }
            UnityEngine.GUILayout.Label(contract.Title);
            string statusText = "ONLINE";
            var statusStyle = okStyle;
            if (allDown) {
              statusText = "OFFLINE";
              statusStyle = disconnectedStyle;
            } else if (!allDown && !allUp) {
              statusText = "PARTIAL";
              statusStyle = partialStyle;
            }
            UnityEngine.GUILayout.Label(statusText, statusStyle);
          }
          if (open_contracts_[contract]) {
            foreach (var connection in connections) {
              if (connection is PointToMultipointConnection point_to_multipoint) {
                var tx = telecom_.network.GetStation(point_to_multipoint.tx_name);
                if (point_to_multipoint.channel_services.Length == 1) {
                  var services = point_to_multipoint.channel_services[0];
                  var rx = telecom_.network.GetStation(point_to_multipoint.rx_names[0]);
                  bool available = services.basic.available;
                  string status = available ? "OK" : "Disconnected";
                  var statusStyle = available ? okStyle : disconnectedStyle;
                  using (new UnityEngine.GUILayout.HorizontalScope()) {
                    UnityEngine.GUILayout.Label(
                        $"From {tx.displaynodeName} to {rx.displaynodeName}: ",
                        connectionLabelStyle);
                    UnityEngine.GUILayout.FlexibleSpace();
                    UnityEngine.GUILayout.Label( $"{status}", statusStyle, GUILayoutWidth(4));
                    connection_inspectors_[connection].RenderButton();
                  }
                } else {
                  using (new UnityEngine.GUILayout.HorizontalScope()) {
                    UnityEngine.GUILayout.Label(
                        $"Broadcast from {tx.displaynodeName} to:",
                        connectionLabelStyle,
                        GUILayoutWidth(15));
                    UnityEngine.GUILayout.FlexibleSpace();
                    connection_inspectors_[connection].RenderButton();
                  }
                }
                for (int i = 0; i < point_to_multipoint.rx_names.Length; ++i) {
                  var services = point_to_multipoint.channel_services[i];
                  bool available = services.basic.available;
                  string status = available ? "OK" : "Disconnected";
                  var statusStyle = available ? okStyle : disconnectedStyle;
                  var rx = telecom_.network.GetStation(point_to_multipoint.rx_names[i]);
                  if (point_to_multipoint.rx_names.Length > 1) {
                    using (new UnityEngine.GUILayout.HorizontalScope()) {
                      UnityEngine.GUILayout.Label(
                        $@"— {rx.displaynodeName}: ", connectionLabelStyle);
                      UnityEngine.GUILayout.FlexibleSpace();
                      UnityEngine.GUILayout.Label( $"{status}", statusStyle, GUILayoutWidth(4));
                    }
                  }
                }
              } else if (connection is DuplexConnection duplex) {
                var trx0 = telecom_.network.GetStation(duplex.trx_names[0]);
                var trx1 = telecom_.network.GetStation(duplex.trx_names[1]);
                bool available = duplex.basic_service.available;
                string status = available ? "OK" : "Disconnected";
                var statusStyle = available ? okStyle : disconnectedStyle;
                using (new UnityEngine.GUILayout.HorizontalScope()) {
                  UnityEngine.GUILayout.Label(
                      $@"Duplex  between {trx0.displaynodeName} and {trx1.displaynodeName}",
                       connectionLabelStyle);
                    UnityEngine.GUILayout.FlexibleSpace();
                    UnityEngine.GUILayout.Label( $"{status}", statusStyle, GUILayoutWidth(4));
                  connection_inspectors_[connection].RenderButton();
                }
              }
            }
          }
        }
        telecom_.network.hide_off_network = show_network;
      }
    }
    UnityEngine.GUI.DragWindow();
  }

  public Dictionary<RealAntennaDigital, AntennaInspector> antenna_inspectors =>
    antenna_inspectors_;

  private Telecom telecom_;
  private readonly Dictionary<Contracts.Contract, bool> open_contracts_ =
      new Dictionary<Contracts.Contract, bool>();
  private readonly Dictionary<Connection, ConnectionInspector> connection_inspectors_ =
      new Dictionary<Connection, ConnectionInspector>();
  private readonly Dictionary<RealAntennaDigital, AntennaInspector> antenna_inspectors_ =
      new Dictionary<RealAntennaDigital, AntennaInspector>();
}

}
