using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;

namespace TauMulticastReferenceCSharp
{
    class TauObjects
    {
        public class AnnouncerDataSerializer
        {
            public DataContractJsonSerializer JsonSerializer;
            public AnnouncerDataSerializer()
            {
                JsonSerializer = new DataContractJsonSerializer(typeof(AnnouncerDataObj));
            }
        }

        [DataContract]
        public class AnnouncerDataObj
        {
            [DataMember(Name = "available_rev")]
            public int AvailableRevision { get; set; }
            [DataMember(Name = "current_rev")]
            public int CurrentRevision { get; set; }
            [DataMember(Name = "cmd_srv_ver")]
            public string CommandServerVersion { get; set; }
            [DataMember(Name = "data_mc_group")]
            public string MulticastDataGroup { get; set; }
            [DataMember(Name = "dbg_mc_group")]
            public string MulticastDebugGroup { get; set; }
            [DataMember(Name = "info_mc_group")]
            public string MulticastMappingGroup { get; set; }
            [DataMember(Name = "log_mc_group")]
            public string MulticastLogsGroup { get; set; }
            [DataMember(Name = "theta_ver")]
            public string ThetaVersion { get; set; }

            public bool Initialized { get; set; }
            public string HubIP { get; set; }

            public override string ToString()
            {
                return String.Format("AvailableRevision: {0}\nCurrentRevision: {1}\nCommandServerVersion: {2}\n" +
                    "MulticastDataGroup: {3}\nMulticastDebugGroup: {4}\nMulticastMappingGroup: {5}\nMulticastLogsGroup: {6}\nThetaVersion: {7}",
                    AvailableRevision, CurrentRevision, CommandServerVersion, MulticastDataGroup, MulticastDebugGroup, MulticastMappingGroup, MulticastLogsGroup, ThetaVersion);
            }
        }

        public class IKBone
        {
            public float x;
            public float y;
            public float z;
        }

        public class Sensor
        {
            public int id;
            public string mapping;
            public bool active;
            public bool bad_coords;
            public float q0;
            public float q1;
            public float q2;
            public float q3;
            public float x;
            public float y;
            public float z;
            public List<IKBone> bones = new List<IKBone>();
        }

        public class Module
        {
            public int serial;
            public byte sensors_active;
            public byte data_integrity;
            public List<Sensor> sensors;

            public Module()
            {
                sensors = new List<Sensor>();
            }

            public string GetMapping() {
                if (sensors.Count > 0)
                {
                    return sensors[0].mapping;
                }
                else {
                    return "";
                }
            }
        }

        public class DataPacket
        {
            public int module_count;
            public List<Module> modules;
            public bool initialized;

            public DataPacket()
            {
                module_count = 0;
                modules = new List<Module>();
            }

            public void ParseUpdate(BinaryReader br, MappingPacket mapping_packet = null)
            {
                //BinaryReader br = new BinaryReader(packet_content_stream);
                module_count = br.ReadByte();

                for (int i = 0; i < module_count; i++)
                {
                    Module cur_module = null;
                    int cur_module_serial = br.ReadUInt16();

                    foreach (Module module in modules)
                    {
                        if (module.serial == cur_module_serial) {
                            cur_module = module;
                            break;
                        }
                    }

                    if (cur_module == null) {

                        cur_module = new Module
                        {
                            serial = cur_module_serial
                        };
                        modules.Add(cur_module);
                    }

                    cur_module.sensors_active = br.ReadByte();
                    cur_module.data_integrity = br.ReadByte();

                    foreach (var sensor in cur_module.sensors)
                    {
                        sensor.active = false;
                    }

                    for (int s = 0; s < 6; s++)
                    {
                        Sensor cur_sensor = null;
                        int cur_sensor_id = cur_module.serial * 16 + s;

                        foreach (var sensor in cur_module.sensors)
                        {
                            if (sensor.id == cur_sensor_id)
                            {
                                cur_sensor = sensor;
                                break;
                            }
                        }

                        if (cur_sensor == null) {
                            cur_sensor = new Sensor
                            {
                                id = cur_sensor_id
                            };
                            cur_module.sensors.Add(cur_sensor);
                        }

                        if (mapping_packet != null && mapping_packet.mapping.TryGetValue(cur_sensor.id, out string temp_map_str))
                        {
                            cur_sensor.mapping = temp_map_str;
                        }

                        cur_sensor.active = (cur_module.sensors_active & (1 << s)) > 0;
                        cur_sensor.bad_coords = (cur_module.data_integrity & (1 << s)) > 0;

                        if (cur_sensor.active)
                        {
                            cur_sensor.q0 = br.ReadSingle();
                            cur_sensor.q1 = br.ReadSingle();
                            cur_sensor.q2 = br.ReadSingle();
                            cur_sensor.q3 = br.ReadSingle();
                            cur_sensor.x = br.ReadSingle();
                            cur_sensor.y = br.ReadSingle();
                            cur_sensor.z = br.ReadSingle();

                            int bonelength = br.ReadInt32();
                            if (bonelength > 0)
                            {
                                while (bonelength > cur_sensor.bones.Count) {
                                    cur_sensor.bones.Add(new IKBone());
                                }

                                for (int bl = 0; bl < bonelength; bl++)
                                {
                                    var cur_bone = cur_sensor.bones[bl];
                                    cur_bone.x = br.ReadSingle();
                                    cur_bone.y = br.ReadSingle();
                                    cur_bone.z = br.ReadSingle();
                                }
                            }
                        }
                    }
                }
                initialized = true;
        }

            public void CopyFrom(DataPacket _packet)
            {
                module_count = _packet.module_count;

                for (int i = 0; i < module_count; i++)
                {
                    Module cur_module = null;
                    int cur_module_serial = _packet.modules[i].serial;

                    foreach (Module module in modules)
                    {
                        if (module.serial == cur_module_serial)
                        {
                            cur_module = module;
                            break;
                        }
                    }

                    if (cur_module == null)
                    {

                        cur_module = new Module
                        {
                            serial = cur_module_serial
                        };
                        modules.Add(cur_module);
                    }


                    cur_module.sensors_active = _packet.modules[i].sensors_active;
                    cur_module.data_integrity = _packet.modules[i].data_integrity;

                    //foreach (var sensor in cur_module.sensors)
                    //{
                    //    sensor.active = false;
                    //}

                    for (int s = 0; s < 6; s++)
                    {
                        Sensor cur_sensor = null;
                        int cur_sensor_id = _packet.modules[i].sensors[s].id;

                        foreach (var sensor in cur_module.sensors)
                        {
                            if (sensor.id == cur_sensor_id)
                            {
                                cur_sensor = sensor;
                                break;
                            }
                        }

                        if (cur_sensor == null)
                        {
                            cur_sensor = new Sensor
                            {
                                id = cur_sensor_id
                            };
                            cur_module.sensors.Add(cur_sensor);
                        }

                        cur_sensor.mapping = _packet.modules[i].sensors[s].mapping;

                        cur_sensor.active = _packet.modules[i].sensors[s].active;
                        cur_sensor.bad_coords = _packet.modules[i].sensors[s].bad_coords;

                        if (cur_sensor.active)
                        {
                            cur_sensor.q0 = _packet.modules[i].sensors[s].q0;
                            cur_sensor.q1 = _packet.modules[i].sensors[s].q1;
                            cur_sensor.q2 = _packet.modules[i].sensors[s].q2;
                            cur_sensor.q3 = _packet.modules[i].sensors[s].q3;
                            cur_sensor.x = _packet.modules[i].sensors[s].x;
                            cur_sensor.y = _packet.modules[i].sensors[s].y;
                            cur_sensor.z = _packet.modules[i].sensors[s].z;

                            int bonelength = _packet.modules[i].sensors[s].bones.Count;
                            if (bonelength > 0)
                            {
                                while (bonelength > cur_sensor.bones.Count)
                                {
                                    cur_sensor.bones.Add(new IKBone());
                                }

                                for (int bl = 0; bl < bonelength; bl++)
                                {
                                    var cur_bone = cur_sensor.bones[bl];
                                    cur_bone.x = _packet.modules[i].sensors[s].bones[bl].x;
                                    cur_bone.y = _packet.modules[i].sensors[s].bones[bl].y;
                                    cur_bone.z = _packet.modules[i].sensors[s].bones[bl].z;
                                }
                            }
                        }
                    }
                }
                initialized = true;
            }

            public override string ToString() {
                string readable_data = "";
                readable_data += String.Format("number of modules: {0}\n", module_count);
                foreach (var m in modules)
                {
                    readable_data += String.Format("module {0}:\n", m.serial.ToString("x"));
                    foreach (var s in m.sensors)
                    {
                        if (s.active == false) {
                            continue;
                        }
                        readable_data += String.Format("  sensor {0}: \n", s.id.ToString("x"));
                        readable_data += String.Format("    active: {0}, bad_coords: {8}, mapping: [{9}] \n    pos({5:+000.00;-000.00} {6:+000.00;-000.00} {7:+000.00;-000.00})\n    quat({1:+000.00;-000.00} {2:+000.00;-000.00} {3:+000.00;-000.00} {4:+000.00;-000.00})\n", s.active, s.q0, s.q1, s.q2, s.q3, s.x, s.y, s.z, s.bad_coords, s.mapping);
                        foreach (var b in s.bones)
                        {
                            readable_data += String.Format("    bone: pos({0:+000.00;-000.00} {1:+000.00;-000.00} {2:+000.00;-000.00})\n", b.x, b.y, b.z);
                        }
                    }
                }
                return readable_data;
            }
        }

        public class MappingPacket
        {
            public Dictionary<int, string> mapping;
            public bool initialized;

            public MappingPacket()
            {
                mapping = new Dictionary<int, string>();
            }

            public void Parse(byte[] received_bytes) {

                var packet = new MappingPacket();
                string converted = Encoding.UTF8.GetString(received_bytes, 0, received_bytes.Length);

                using (StringReader reader = new StringReader(converted))
                {
                    string line = string.Empty;
                    do
                    {
                        line = reader.ReadLine();
                        if (line != null)
                        {
                            var spl_line = line.Split('=');
                            var map = spl_line[1].Replace("\"", "");
                            var sensid = Convert.ToInt32(spl_line[0], 16);

                            if (!mapping.ContainsKey(sensid))
                            {
                                mapping.Add(sensid, map);
                            }
                            else
                            {
                                mapping[sensid] = map;
                            }
                        }

                    } while (line != null);
                }
                initialized = true;
            }

            public override string ToString()
            {
                string readable_data = "";

                foreach (KeyValuePair<int, string> entry in mapping)
                {
                    readable_data += String.Format("{0}={1}\n", entry.Key, entry.Value);
                }

                return readable_data;
            }
        }
    }
}
