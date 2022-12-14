using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AzangaraUnpacker
{
  internal static class Program
  {

    public static IEnumerable<T> PadRight<T>(this IEnumerable<T> source, int length, T value = default)
    {
      int i = 0;
      foreach(var item in source.Take(length)) 
      {
        yield return item;
        i++;
      }
      for( ; i < length; i++)
        yield return value;
    }
    
    static int Fail(string message)
    {
      Console.WriteLine(message);
      Console.WriteLine("Нажмите любую клавишу чтобы выйти... Press any key to exit...");
      Console.ReadKey();
      return 1;
    }
    
    static string GetRelativePath(string filespec, string folder)
    {
      Uri pathUri = new Uri(filespec);
      if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
      {
        folder += Path.DirectorySeparatorChar;
      }
      Uri folderUri = new Uri(folder);
      return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString());
    }
    
    public static int Main(string[] args)
    {
      Console.OutputEncoding = Encoding.UTF8;
      
      if (args.Length < 1) return Fail("Для того чтобы запаковать папку или распаковать .pak - перетащите файл на AzangaraUnpacker.exe\nIn order to pack folder, or unpack .pak - drag and drop the file on the AzangaraUnpacker.exe");

      if (File.Exists(args[0]))
      {
        var basePath = Path.Combine(Path.GetDirectoryName(args[0]) ?? ".", Path.GetFileNameWithoutExtension(args[0]));
        
        using (var file = File.OpenRead(args[0]))
        using (var reader = new BinaryReader(file))
        {
          var buf = reader.ReadBytes(4);

          if (buf[0] != 'P' || buf[1] != 'A' || buf[2] != 'C' || buf[3] != 'K')
          {
            return Fail("Неверный формат файла (несовпадение заголовка)\nInvalid file format (unknown header)");
          }
          
          if (reader.ReadInt16() != 0x101)
          {
            return Fail("Неверный формат файла (несоответствие версии)\nInvalid file format (version mismatch)");
          }

          var tableSize = reader.ReadInt32();
          var entries = tableSize / 0x88;

          for (var i = 0; i < entries; i++)
          {
            var nameBuf = reader.ReadBytes(128);
            var name = Encoding.UTF8.GetString(nameBuf.TakeWhile(e => e != 0).ToArray());
            var offset = reader.ReadInt32();
            var size = reader.ReadInt32();
            var pos = reader.BaseStream.Position;
            reader.BaseStream.Seek(offset, SeekOrigin.Begin);

            var path = Path.Combine(basePath, name);
            var directoryPath = Path.GetDirectoryName(path);
            if (directoryPath != null) Directory.CreateDirectory(directoryPath);
            
            Console.WriteLine($"Unpacking {name}...");

            File.WriteAllBytes(path, reader.ReadBytes(size));
            reader.BaseStream.Seek(pos, SeekOrigin.Begin);
          }
        }
      } else if (Directory.Exists(args[0]))
      {
        var path = args[0].Replace("\\", "/");
        if (path.EndsWith("/")) path = path.Substring(0, path.Length - 1);
        
        var table = new List<byte>();
        var body = new List<byte>();
        var files = Directory.EnumerateFiles(args[0], "*.*", SearchOption.AllDirectories).ToArray();
        var baseOffset = files.Length * 0x88 + 10;
        
        for (var index = 0; index < files.Length; index++)
        {
          var file = files[index];
          var data = File.ReadAllBytes(file);
          var fileKey = GetRelativePath(file, args[0]).Replace("\\", "/");
          Console.WriteLine($"Packing {fileKey}...");
          table.AddRange(Encoding.UTF8.GetBytes(fileKey).PadRight(128));
          table.AddRange(BitConverter.GetBytes((int) (baseOffset + body.Count)));
          table.AddRange(BitConverter.GetBytes((int) (data.Length)));
          body.AddRange(data);
        }

        var pakFile = Path.Combine(Path.GetDirectoryName(path) ?? ".", Path.GetFileName(path) + ".pak");
        if (File.Exists(pakFile)) File.Delete(pakFile);

        using (var stream = File.OpenWrite(pakFile))
        using (var writer = new BinaryWriter(stream))
        {
          writer.Write(new[] { 'P', 'A', 'C', 'K' });
          writer.Write((short) 0x101);
          writer.Write((int) (table.Count));
          writer.Write(table.ToArray());
          writer.Write(body.ToArray());
        }
      }
      else
      {
        return Fail("Файл не найден\nFile not found");
      }
      
      Console.WriteLine("Готово! Done!");
      Console.WriteLine("Нажмите любую клавишу чтобы выйти... Press any key to exit...");
      Console.ReadKey();
      return 0;
    }
  }
}

/*
struct FileDefinition {
    char data[128];
    s32 offset;
    s32 size;
};

struct Pak {
    char magic[4];
    s16 version;
    s32 dataSize;
    FileDefinition fileDefinition[dataSize / 136];
};

Pak pak @ 0x00;*/
