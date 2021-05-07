# BlobCompressionTools
Azure function to unrar files from Azure storage.

To call the function

POST http://localhost:7071/api/Unrar
Content-Type: application/json

`{`
  `"fileName":"Azure Synapse Analytics L300.rar"`
`}`







Local call passing fileName, source and destination containers

POST http://localhost:7071/api/Unrar
Content-Type: application/json

`{`
  `"fileName":"Azure Synapse Analytics L300.rar",`
  `"containerSource":"source",`
  `"containerTarget":"destination"`
`}`

Local call passing fileName, source and destination containers and set useManagedIdentity to false

POST http://localhost:7071/api/Unrar
Content-Type: application/json

`{`
  `"fileName":"Azure Synapse Analytics L300.rar",`
  `"containerSource":"source",`
  `"containerTarget":"destination",`
  `"useManagedIdentity":false`
`}`

If not passing the source & destination containers, then add them in the function settings. The list of settings are:

-   ContainerNameSource: Optional if it's passed to the function call
-   ContainerNameTarget: Optional if it's passed to the function call
-   StorageAccountName: Mandatory
-   StorageAccountConnectionString: Optional unless useManagedIdentity is explicitly set to false. The default is true



*Note: Don't use connection string as possible. Using managed identities is more secure as it doesn't involve secrets*



