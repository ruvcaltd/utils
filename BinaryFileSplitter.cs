


namespace BinaryFileSplitter
{
    class BinaryFileSplitter
    {
        // Split a file into two parts
        public static void SplitFile(string inputFile, string part1File, string part2File)
        {
            byte[] fileBytes = File.ReadAllBytes(inputFile);

            int mid = fileBytes.Length / 2;

            // First half
            byte[] part1 = new byte[mid];
            Array.Copy(fileBytes, 0, part1, 0, mid);

            // Second half
            byte[] part2 = new byte[fileBytes.Length - mid];
            Array.Copy(fileBytes, mid, part2, 0, part2.Length);

            File.WriteAllBytes(part1File, part1);
            File.WriteAllBytes(part2File, part2);

            Console.WriteLine($"File split into '{part1File}' and '{part2File}'");
        }

        // Stitch parts back into one file
        public static void StitchFile(string part1File, string part2File, string outputFile)
        {
            byte[] part1 = File.ReadAllBytes(part1File);
            byte[] part2 = File.ReadAllBytes(part2File);

            byte[] fullFile = new byte[part1.Length + part2.Length];
            Buffer.BlockCopy(part1, 0, fullFile, 0, part1.Length);
            Buffer.BlockCopy(part2, 0, fullFile, part1.Length, part2.Length);

            File.WriteAllBytes(outputFile, fullFile);

            Console.WriteLine($"Files stitched into '{outputFile}'");
        }

        static void Main(string[] args)
        {
            string originalFile = "C:\\tmp\\qdrant-x86_64-pc-windows-msvc.zip";      // Replace with your file
            string part1 = "C:\\tmp\\part1.bin";
            string part2 = "C:\\tmp\\part2.bin";
            string stitchedFile = "reconstructed.bin";

            // Split
            SplitFile(originalFile, part1, part2);

            // Stitch
            StitchFile(part1, part2, stitchedFile);
        }
    }

}
