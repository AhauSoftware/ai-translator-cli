# AI .resx and .json translator

## Setup / Build
```cd ResxTranslator```

```dotnet build```

## Run

You can set your OpenAI Api Key and your preferred AI Model in the ResxTranslator/App.config or set them in the execution code according to the [Avaliable Parameters](#available-parameters).

```cd ResxTranslator``` (if not already in the directory)

```dotnet run -i <input file or folder> -l <language code (ex: pt)> ```

## Available Parameters

The app can take the following parameters:

  ```-i, --input       Input folder or file path.```

  ```-l, --language    Language code for translation.```

  ```-k, --apikey      OpenAI API key.```

  ```-m, --model       AI Model to use.```

  ```--retry           Retry translation on failed files.```

## Retry failed translations

The translation isn't always successful. When the process mentioned failed translations in the end, you can run the following code:

```dotnet run --retry```

Doing so will proceed to translate the previously failed translations. Repeat this step as many times as necessary until all files become successfully translated.
