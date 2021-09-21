FROM hjerpbakk/dotnet-script:latest

COPY ./main.csx /

ENTRYPOINT ["dotnet-script", "../main.csx", "--", "/scripts"]