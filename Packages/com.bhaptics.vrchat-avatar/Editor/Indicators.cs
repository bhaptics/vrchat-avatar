namespace bHaptics.VRChatAvatar
{
    /// Names for the indicator of a PC node, one per node. A merged node takes its lowest contract number: VestFront 12+16 is VestFront_12.
    public static class Indicators
    {
        /// Returns the indicator's bone name, such as VestFront_12; no slash, since a slash splits animation paths.
        public static string GetBoneName(DeviceDefinition.Node node)
        {
            var (segment, number) = GetSegmentAndNumber(node);
            return $"{segment}_{number}";
        }

        /// Returns the synced Bool that shows the indicator to everyone, such as bHaptics/VestFront/12.
        public static string GetParameterName(DeviceDefinition.Node node)
        {
            var (segment, number) = GetSegmentAndNumber(node);
            return $"bHaptics/{segment}/{number}";
        }

        // Addresses read bOSC/<schema>/<Segment>/<n>/<self|others>.
        private static (string segment, int number) GetSegmentAndNumber(DeviceDefinition.Node node)
        {
            string segment = null;
            var number = int.MaxValue;
            foreach (var receiver in node.receivers)
            {
                var parts = receiver.address.Split('/');
                var n = int.Parse(parts[3]);
                if (n < number)
                {
                    segment = parts[2];
                    number = n;
                }
            }

            return (segment, number);
        }
    }
}
