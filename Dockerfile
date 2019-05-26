FROM hjerpbakk/dotnet-script

COPY ./main.csx .

ENTRYPOINT ["dotnet-script", "main.csx", "--", "/scripts"]