using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;

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
            public string id;
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
            public string serial;
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

            public DataPacket()
            {
                modules = new List<Module>();
            }

            public static DataPacket Parse(MemoryStream packet_content_stream)
            {
                var packet = new DataPacket();

                //int bytecounter = 0;
                //MemoryStream stream = new MemoryStream();
                BinaryReader br = new BinaryReader(packet_content_stream);
                packet.module_count = br.ReadByte();


                for (int i = 0; i < packet.module_count; i++)
                {
                    var module = new Module
                    {
                        serial = br.ReadUInt16().ToString("x"),
                        sensors_active = br.ReadByte(),
                        data_integrity = br.ReadByte()

                    };

                    for (int s = 0; s < 6; s++)
                    {
                        var sensor = new Sensor
                        {
                            id = module.serial + s.ToString(),
                            active = (module.sensors_active & (1 << s)) > 0,
                            bad_coords = (module.data_integrity & (1 << s)) > 0
                        };

                        if (sensor.active)
                        {
                            sensor.q0 = br.ReadSingle();
                            sensor.q1 = br.ReadSingle();
                            sensor.q2 = br.ReadSingle();
                            sensor.q3 = br.ReadSingle();
                            sensor.x = br.ReadSingle();
                            sensor.y = br.ReadSingle();
                            sensor.z = br.ReadSingle();

                            int bonelength = br.ReadInt32();
                            if (bonelength > 0)
                            {
                                for (int bl = 0; bl < bonelength; bl++)
                                {
                                    var bone = new IKBone();
                                    bone.x = br.ReadSingle();
                                    bone.y = br.ReadSingle();
                                    bone.z = br.ReadSingle();
                                    sensor.bones.Add(bone);
                                }
                            }
                            module.sensors.Add(sensor);
                        }
                    }
                    packet.modules.Add(module);

                }
                return packet;
            }

            public override string ToString() {
                string readable_data = "";
                readable_data += String.Format("number of modules: {0}\n", module_count);
                foreach (var m in modules)
                {
                    readable_data += String.Format("module {0}:\n", m.serial);
                    foreach (var s in m.sensors)
                    {
                        readable_data += String.Format("  sensor {0}: \n", s.id);
                        readable_data += String.Format("    active: {0}, bad_coords: {8}, \n    pos({5:+000.00;-000.00} {6:+000.00;-000.00} {7:+000.00;-000.00})\n    quat({1:+000.00;-000.00} {2:+000.00;-000.00} {3:+000.00;-000.00} {4:+000.00;-000.00})\n", s.active, s.q0, s.q1, s.q2, s.q3, s.x, s.y, s.z, s.bad_coords);
                        foreach (var b in s.bones)
                        {
                            readable_data += String.Format("    bone: pos({0:+000.00;-000.00} {1:+000.00;-000.00} {2:+000.00;-000.00})\n", b.x, b.y, b.z);
                        }
                    }
                }
                return readable_data;
            }
        }
    }
}
