﻿using System;
using System.Linq;
using RealAntennas;
using UnityEngine;

namespace σκοπός {

using static Routing.NetworkUsage.SpectrumBreakdown.SingleUsage.Kind;

internal class ConnectionInspector : principia.ksp_plugin_adapter.SupervisedWindowRenderer {
  public ConnectionInspector(Telecom telecom, Connection connection)
      : base(telecom) {
    telecom_ = telecom;
    connection_ = connection;
    if (connection_ is PointToMultipointConnection point_to_multipoint &&
        point_to_multipoint.rx_names.Length > 1) {
      receiver_open_ = new bool[point_to_multipoint.rx_names.Length];
    }
  }

  protected override string Title => "Connection inspector";

  private void ShowCircuit(Routing.Circuit circuit, bool available,
                           bool exclusive) {
    if (circuit == null) {
      return;
    }
    using (new UnityEngine.GUILayout.HorizontalScope()) {
      using (new UnityEngine.GUILayout.VerticalScope()) {
        ShowChannel(circuit.forward, available, exclusive);
      }
      using (new UnityEngine.GUILayout.VerticalScope()) {
        ShowChannel(circuit.backward, available, exclusive);
      }
    }
  }

  private string TxPowerReport(Routing.OrientedLink link, bool available,
                          bool exclusive, out GUIStyle style) {
    style = UnityEngine.GUI.skin.label;
    double this_link_tx_power =
        link.tx_antenna.PowerDrawLinear * 1e-3 *
        link.TxPowerUsageFromDataRate(connection_.data_rate);
    double max_tx_power = link.tx_antenna.PowerDrawLinear * 1e-3;
    if (exclusive && telecom_.network.routing_.IsLimited(link.tx)) {
      Routing.NetworkUsage.PowerBreakdown tx_power_usage =
          telecom_.network.routing_.usage.SourcedTxPowerUsage(link.tx_antenna);
      double used_tx_power = link.tx_antenna.PowerDrawLinear * 1e-3 *
          tx_power_usage.power;
      if (available) {
        return $@"Draws: {
                   this_link_tx_power:N1} W / Total: {
                   used_tx_power:N1} W / Max: {
                   max_tx_power:N1} W";
      } else {
        if (connection_ is PointToMultipointConnection &&
            tx_power_usage.usages.Any(
               usages => usages.Any(
               usage => usage.link.connection == connection_ &&
                        usage.link.link.rx_antenna == link.rx_antenna))) {
          return $@"Draws: {
                     this_link_tx_power:N1} W / Total: {
                     used_tx_power:N1} W / Max: {
                     max_tx_power:N1} W";
        } else {
          double remaining_power = max_tx_power;
          if (connection_ is PointToMultipointConnection) {
            foreach (var usages in tx_power_usage.usages) {
              if (usages.First().link.connection != connection_) {
                remaining_power -= link.tx_antenna.PowerDrawLinear * 1e-3 *
                    usages.Max(usage => usage.power);
              }
            }
          } else {
           remaining_power -= used_tx_power;
          }
          if (this_link_tx_power > remaining_power) {
            style = principia.ksp_plugin_adapter.Style.Warning(
                UnityEngine.GUI.skin.label);
            return $@"Power-limited, would need: {
                       this_link_tx_power:N1} W / Available: {
                       remaining_power:N1} W / Max: {
                       max_tx_power:N1} W";
          } else {
            return $@"Would draw: {
                       this_link_tx_power:N1} W / Available: {
                       remaining_power:N1} W / Max: {
                       max_tx_power:N1} W";
          }
        }
      }
    } else {
      if (available) {
        return $@"Draws: {
                   this_link_tx_power:N1} W / Max: {
                   max_tx_power:N1} W";
      } else {
        if (this_link_tx_power > max_tx_power) {
          style = principia.ksp_plugin_adapter.Style.Warning(
              UnityEngine.GUI.skin.label);
            return $@"Power-limited, would need: {
                       this_link_tx_power:N1} W / Max: {
                       max_tx_power:N1} W";
        } else {
            return $@"Would draw: {
                       this_link_tx_power:N1} W / Max: {
                       max_tx_power:N1} W";
        }
      }
    }
  }

  private string SpectrumReport(
      RealAntennaDigital antenna, Routing.OrientedLink link, bool available,
      bool exclusive, out GUIStyle style) {
    style = UnityEngine.GUI.skin.label;
    double this_link_spectrum =
        link.SpectrumUsageFromDataRate(connection_.data_rate);
    double total_spectrum = link.band.ChannelWidth;
    if (exclusive &&
        telecom_.network.routing_.IsLimited((RACommNode)antenna.ParentNode)) {
      Routing.NetworkUsage.SpectrumBreakdown used_spectrum =
          telecom_.network.routing_.usage.SourcedSpectrumUsage(antenna);
      if (available) {
        return $@"Uses: {
                   RATools.PrettyPrint(this_link_spectrum)}Hz / Total: {
                   RATools.PrettyPrint(used_spectrum.spectrum)}Hz / Max: {
                   RATools.PrettyPrint(total_spectrum)}Hz";
      } else {
        if (connection_ is PointToMultipointConnection &&
            used_spectrum.usages.Any(
               usages => usages.Any(
               usage => usage.link.connection == connection_ &&
                  ((link.rx_antenna == antenna && usage.kind == Receive) ||
                    (link.tx_antenna == antenna && usage.kind == Transmit))))) {
          return $@"Uses: {
                     RATools.PrettyPrint(this_link_spectrum)}Hz / Total: {
                     RATools.PrettyPrint(used_spectrum.spectrum)}Hz / Max: {
                     RATools.PrettyPrint(total_spectrum)}Hz";
        } else {
          double remaining_spectrum = total_spectrum - used_spectrum.spectrum;
          if (this_link_spectrum > remaining_spectrum) {
            style = principia.ksp_plugin_adapter.Style.Warning(
                UnityEngine.GUI.skin.label);
            return $@"Bandwidth-limited, would need: {
                     RATools.PrettyPrint(this_link_spectrum)}Hz / Available: {
                     RATools.PrettyPrint(remaining_spectrum)}Hz / Max: {
                     RATools.PrettyPrint(total_spectrum)}Hz";
          } else {
            return $@"Would use: {
                       RATools.PrettyPrint(this_link_spectrum)}Hz / Available: {
                       RATools.PrettyPrint(remaining_spectrum)}Hz / Max: {
                       RATools.PrettyPrint(total_spectrum)}Hz";
          }
        }
      }
    } else {
      if (available) {
        return $@"Uses: {
                   RATools.PrettyPrint(this_link_spectrum)}Hz / Max: {
                   RATools.PrettyPrint(total_spectrum)}Hz";
      } else {
        if (this_link_spectrum > total_spectrum) {
          style = principia.ksp_plugin_adapter.Style.Warning(
              UnityEngine.GUI.skin.label);
            return $@"Bandwidth-limited, would need: {
                     RATools.PrettyPrint(this_link_spectrum)}Hz / Max: {
                     RATools.PrettyPrint(total_spectrum)}Hz";
        } else {
            return $@"Would use: {
                       RATools.PrettyPrint(this_link_spectrum)}Hz / Max: {
                       RATools.PrettyPrint(total_spectrum)}Hz";
        }
      }
    }
  }

  private void ShowChannel(Routing.Channel channel,
                           bool available,
                           bool exclusive) {
    if (channel == null) {
        return;
      }
    UnityEngine.GUILayout.Label(channel.links[0].tx.displayName);
    foreach (var link in channel.links) {
      using (new UnityEngine.GUILayout.HorizontalScope()) {
        UnityEngine.GUILayout.Label($"Tx {link.tx_antenna.Name}");
        if (telecom_.network.routing_.IsLimited(link.tx)) {
          if (!telecom_.main_window_.antenna_inspectors.TryGetValue(
                  link.tx_antenna, out var inspector)) {
            inspector = new AntennaInspector(telecom_, link.tx_antenna);
            telecom_.main_window_.antenna_inspectors[link.tx_antenna] = inspector;
          }
          inspector.RenderButton();
        }
      }
      UnityEngine.GUILayout.Label(
          SpectrumReport(link.tx_antenna, link, available, exclusive,
                         out var style),
          style);
      UnityEngine.GUILayout.Label(
          TxPowerReport(link, available, exclusive, out style),
          style);
      UnityEngine.GUILayout.Label(
          $"↓ {link.length / 299792458 * 1000:N0} ms");
      using (new UnityEngine.GUILayout.HorizontalScope()) {
        UnityEngine.GUILayout.Label($"Rx {link.rx_antenna.Name}");
        if (telecom_.network.routing_.IsLimited(link.rx)) {
          if (!telecom_.main_window_.antenna_inspectors.TryGetValue(
                  link.rx_antenna, out var inspector)) {
            inspector = new AntennaInspector(telecom_, link.rx_antenna);
            telecom_.main_window_.antenna_inspectors[link.rx_antenna] = inspector;
          }
          inspector.RenderButton();
        }
      }
      UnityEngine.GUILayout.Label(SpectrumReport(link.rx_antenna, link,
                                                 available, exclusive,
                                                 out style),
                                  style);
      UnityEngine.GUILayout.Label(link.rx.displayName);
    }
  }

  protected override void RenderWindowContents(int window_id) {
    string desired_data_rate = RATools.PrettyPrintDataRate(connection_.data_rate);
    if (connection_ is PointToMultipointConnection point_to_multipoint) {
      UnityEngine.GUILayout.Label(
          $"Data rate: {desired_data_rate}");
      UnityEngine.GUILayout.Label(
          $"Latency limit: {connection_.latency_limit * 1000:N0} ms");
      foreach (double latency in
               point_to_multipoint.channel_services[0].improved_by_latency.Keys) {
        UnityEngine.GUILayout.Label(
            $"Improved service latency: {latency * 1000:N0} ms");
      }

      var tx = telecom_.network.GetStation(point_to_multipoint.tx_name);
      if (point_to_multipoint.channel_services.Length == 1) {
        var services = point_to_multipoint.channel_services[0];
        var rx = telecom_.network.GetStation(point_to_multipoint.rx_names[0]);
        bool available = services.basic.available;
        string status = available ? $"Connected" : "Disconnected";
        UnityEngine.GUILayout.Label(
          $"Transmission from {tx.displaynodeName} to {rx.displaynodeName}: {status}");
      } else {
        UnityEngine.GUILayout.Label($"Broadcast to:");

      }
      for (int i = 0; i < point_to_multipoint.rx_names.Length; ++i) {
        var services = point_to_multipoint.channel_services[i];
        bool available = services.basic.available;
        string status = available ? "Connected" : "Disconnected";
        var rx = telecom_.network.GetStation(point_to_multipoint.rx_names[i]);
        if (receiver_open_ != null) {
          using (new UnityEngine.GUILayout.HorizontalScope()) {
            if (UnityEngine.GUILayout.Button(
                  receiver_open_[i] ? "−" : "+", GUILayoutWidth(1))) {
              receiver_open_[i] = !receiver_open_[i];
              ScheduleShrink();
              return;
            }
            UnityEngine.GUILayout.Label($"{rx.displaynodeName}: {status}");
          }
        }
        if (receiver_open_?[i] != false) {
          if (available) {
            ShowChannel(services.channel, available,
                        point_to_multipoint.exclusive);
          } else {
            Routing.Channel[] channels;
            bool capacity_limited = false;
            if (connection_.exclusive) {
              telecom_.network.routing_.FindChannelsInIsolation(
                  tx.Comm,
                  new[]{rx.Comm},
                  connection_.latency_limit,
                  connection_.data_rate,
                  out channels);
              if (channels[0] != null) {
                capacity_limited = true;
                UnityEngine.GUILayout.Label(
                      $"→ Limited by capacity: available in isolation:");
                ShowChannel(channels[0], available,
                            point_to_multipoint.exclusive);
              }
            }
            if (!capacity_limited) {
              telecom_.network.routing_.FindChannelsInIsolation(
                  tx.Comm,
                  new[]{rx.Comm},
                  latency_limit: double.PositiveInfinity,
                  connection_.data_rate,
                  out channels);
              bool purely_latency_limited = channels[0] != null;
              if (purely_latency_limited) {
                UnityEngine.GUILayout.Label(
                    $"→ Latency-limited: available at {channels[0].latency * 1000:N0} ms:");
                  ShowChannel(channels[0], available,
                              point_to_multipoint.exclusive);
              }
              telecom_.network.routing_.FindChannelsInIsolation(
                  tx.Comm,
                  new[]{rx.Comm},
                  connection_.latency_limit,
                  data_rate: 0,
                  out channels);
              bool purely_rate_limited = channels[0] != null;
              if (purely_rate_limited) {
                string max_data_rate = RATools.PrettyPrintDataRate(
                    (from link in channels[0].links
                      select link.max_data_rate).Min());
                UnityEngine.GUILayout.Label(
                    $"→ Limited by data rate: available at {max_data_rate}");
                ShowChannel(channels[0], available,
                            point_to_multipoint.exclusive);
              }
              if (!purely_rate_limited && !purely_latency_limited) {
                telecom_.network.routing_.FindChannelsInIsolation(
                    tx.Comm,
                    new[]{rx.Comm},
                    latency_limit: double.PositiveInfinity,
                    data_rate: 0,
                    out channels);
                if (channels[0] != null) {
                  string max_data_rate = RATools.PrettyPrintDataRate(
                      (from link in channels[0].links
                        select link.max_data_rate).Min());
                  UnityEngine.GUILayout.Label(
                      "→ Limited by both latency and data rate: available at " +
                      $"{max_data_rate}, {channels[0].latency * 1000:N0} ms");
                  ShowChannel(channels[0], available,
                              point_to_multipoint.exclusive);
                }
              }
            }
          }
        }
      }
    } else if (connection_ is DuplexConnection duplex) {
      UnityEngine.GUILayout.Label(
          $"One-way data rate: {desired_data_rate}");
      UnityEngine.GUILayout.Label(
          $"Round-trip latency limit: {connection_.latency_limit * 1000:N0} ms");
      foreach (double latency in
               duplex.improved_service_by_latency.Keys) {
        UnityEngine.GUILayout.Label(
            $"Improved service round-trip latency: {latency * 1000:N0} ms");
      }
      var trx0 = telecom_.network.GetStation(duplex.trx_names[0]);
      var trx1 = telecom_.network.GetStation(duplex.trx_names[1]);
      bool available = duplex.basic_service.available;
      string status = available
          ? $"Connected ({duplex.actual_latency * 1000:N0} ms)"
          : "Disconnected";
      UnityEngine.GUILayout.Label(
          $@"Duplex between {trx0.displaynodeName} and {trx1.displaynodeName}: {status}");
      if (available) {
        ShowCircuit(duplex.circuit, available, duplex.exclusive);
      } else {
        Routing.Circuit circuit;
        bool capacity_limited = false;
        if (connection_.exclusive) {
          circuit = telecom_.network.routing_.FindCircuitInIsolation(
              trx0.Comm,
              trx1.Comm,
              connection_.latency_limit,
              connection_.data_rate);
          if (circuit != null) {
            capacity_limited = true;
            UnityEngine.GUILayout.Label(
                  $"→ Limited by capacity: available in isolation:");
            ShowCircuit(circuit, available, duplex.exclusive);
          } else {
            telecom_.network.routing_.FindChannelsInIsolation(
                trx0.Comm,
                new[] { trx1.Comm },
                connection_.latency_limit,
                connection_.data_rate,
                out Routing.Channel[] forward);
            telecom_.network.routing_.FindChannelsInIsolation(
                trx1.Comm,
                new[] { trx0.Comm },
                connection_.latency_limit,
                connection_.data_rate,
                out Routing.Channel[] backward);
            if (forward[0] != null && backward[0] != null) {
              capacity_limited = true;
              UnityEngine.GUILayout.Label(
                    $"→ Limited by capacity: available in simplex:");
              ShowCircuit(new Routing.Circuit(forward[0], backward[0]),
                          available, duplex.exclusive);
            }
          }
        }
        if (!capacity_limited) {
          circuit = telecom_.network.routing_.FindCircuitInIsolation(
              trx0.Comm,
              trx1.Comm,
              round_trip_latency_limit: double.PositiveInfinity,
              connection_.data_rate);
          bool purely_latency_limited = circuit != null;
          if (purely_latency_limited) {
            UnityEngine.GUILayout.Label(
                $@"→ Latency-limited: available at {
                    circuit.round_trip_latency * 1000:N0} ms");
            ShowCircuit(circuit, available, duplex.exclusive);
          }
          circuit = telecom_.network.routing_.FindCircuitInIsolation(
              trx0.Comm,
              trx1.Comm,
              connection_.latency_limit,
              one_way_data_rate: 0);
          bool purely_rate_limited = circuit != null;
          if (purely_rate_limited) {
            string max_data_rate = RATools.PrettyPrintDataRate(
                Math.Min(
                    (from link in circuit.forward.links
                      select link.max_data_rate).Min(),
                    (from link in circuit.backward.links
                      select link.max_data_rate).Min()));
            UnityEngine.GUILayout.Label(
                $"→ Limited by data rate: available at {max_data_rate}");
            ShowCircuit(circuit, available, duplex.exclusive);
          }
          if (!purely_rate_limited && !purely_latency_limited) {
            circuit = telecom_.network.routing_.FindCircuitInIsolation(
                trx0.Comm,
                trx1.Comm,
                round_trip_latency_limit: double.PositiveInfinity,
                one_way_data_rate: 0);
            if (circuit != null) {
              string max_data_rate = RATools.PrettyPrintDataRate(
                  Math.Min(
                      (from link in circuit.forward.links
                        select link.max_data_rate).Min(),
                      (from link in circuit.backward.links
                        select link.max_data_rate).Min()));
              UnityEngine.GUILayout.Label(
                  "→ Limited by both latency and data rate: available at " +
                  $"{max_data_rate}, {circuit.round_trip_latency * 1000:N0} ms");
              ShowCircuit(circuit, available, duplex.exclusive);
            }
          }
        }
      }
    }
    UnityEngine.GUI.DragWindow();
  }

  public void RenderButton() {
    if (UnityEngine.GUILayout.Button("Inspect…", UnityEngine.GUILayout.ExpandWidth(false))) {
      Toggle();
    }
  }

    private Telecom telecom_;
    private Connection connection_;
    private bool[] receiver_open_;
}

}
