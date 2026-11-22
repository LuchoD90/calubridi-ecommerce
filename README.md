# Calubridi · Tela en casa

Tienda de costura por encargo sobre ASP.NET Core 10, Entity Framework Core y PostgreSQL. La misma aplicación sirve la interfaz y la API.

## Probar en local

Instalá el SDK de .NET 10 y creá una base de datos PostgreSQL vacía. Desde la raíz del repositorio:

```powershell
Set-Location backend/Calubridi.Api
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5080"
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Database=calibridi;Username=calibridi;Password=TU_CLAVE_POSTGRES"
$env:BootstrapAdmin__Email = "admin@tu-dominio.example"
$env:BootstrapAdmin__Password = "elegí una contraseña propia con 12 caracteres, mayúscula y número"
$env:Store__SeedDemo = "true"
dotnet run --no-launch-profile -- --initialize
Remove-Item Env:BootstrapAdmin__Password
dotnet run --no-launch-profile
```

Abrí [http://localhost:5080](http://localhost:5080). Usá el correo de administración que configuraste para entrar a **Mi cuenta → Panel del taller**. El administrador inicial se crea de forma explícita. Si la dirección ya pertenece a una cuenta de cliente, elegí otra para el administrador. Los diseños, imágenes y precios de demostración se cargan si activás `Store__SeedDemo` y el catálogo está vacío. Para una base real, ejecutá la inicialización con `Store__SeedDemo` desactivado.

## Flujo del taller

- **Catálogo**. Creá un borrador con nombre, categoría, precio y plazo estimado. Subí una foto de portada. Si corresponde, agregá telas con su precio, colores, fotos por variante y videos. Activá **Mostrar en la colección** para publicar el diseño.
- **Encargos**. El cliente elige sus opciones, arma el carrito e indica sus datos, medidas o preferencias. El servidor verifica que el precio y la combinación coincidan con el catálogo. Se registra el encargo como **Recibido**; desde el panel se puede confirmar y avanzar hasta **Entregado**.
- **Cobros y compras**. El pago y la entrega se coordinan con el taller. Marcá el cobro después de recibirlo y registrá compras de insumos con fecha, proveedor e importe. El panel filtra por fecha del pedido, fecha del cobro o fecha de compra según el indicador. Contra entrega y transferencia son decisiones operativas del taller. No se solicitan números de tarjeta.
- **Cuenta**. El registro local pide nombre de usuario, correo y contraseña. El cliente puede guardar favoritos y ver sus propios pedidos. La contraseña la administra ASP.NET Identity. El correo de la cuenta local se guarda como dato de contacto y no se verifica por correo en esta versión.

## Ingreso con Google y publicación

1. Creá un cliente OAuth web en Google Cloud y registrá la URI **exacta** del sitio seguida de `/signin-google`. Para una prueba local con el ejemplo anterior sería `http://localhost:5080/signin-google`.
2. Configurá `Google__ClientId` y `Google__ClientSecret` como secretos del entorno. El botón aparece al configurar ambos. Las cuentas de Google se identifican por el ID del proveedor y el correo verificado. Una cuenta creada con contraseña conserva su método de acceso aunque use el mismo correo.
3. Configurá HTTPS, la conexión de PostgreSQL y el almacenamiento persistente y respaldado de `wwwroot/uploads` y de las claves de ASP.NET Data Protection. Ejecutá `dotnet Calubridi.Api.dll --initialize` como paso de instalación y retirale al proceso web las credenciales de arranque y los permisos de migración. Si terminás HTTPS en un proxy, configurá el esquema HTTPS de origen y limitá los encabezados reenviados a ese proxy de confianza.
4. `Store__Currency` usa ARS por defecto. Definí la moneda antes de cargar precios. Su valor se guarda en cada pedido y compra ya registrados. El panel usa horario de Buenos Aires.

El esquema nuevo tiene una migración de EF que conserva las tablas existentes. Las consultas recogen los valores del usuario mediante LINQ y parámetros de EF. Las escrituras requieren un token antifalsificación y los permisos se comprueban del lado del servidor. Las fotos y los videos del catálogo se sirven como archivos públicos.

### Contenedor de prueba

El `Dockerfile` de la raíz publica la aplicación para Linux en el puerto 8080. Pasale `ConnectionStrings__DefaultConnection` y los secretos como variables del entorno en el momento de ejecutar el contenedor. Ejecutá el contenedor una vez con `--initialize` para aplicar las migraciones y crear el administrador, y luego iniciá el servicio sin ese argumento. La imagen excluye los archivos cargados localmente. Las dos fotos del catálogo de prueba están incluidas para poder probar Render desde el primer inicio.

Un servicio Free de Render puede ejecutar la imagen con PostgreSQL para pruebas temporales. Su disco es efímero, así que las cargas locales se pierden al reiniciar. Para una instalación con catálogo real, usá almacenamiento persistente para las cargas y una base con copias de seguridad.

## Marca y consultas

La sección **Arreglos** y el botón de WhatsApp abren el número comercial publicado por Calubridi. El formulario exige un teléfono con código de área. Los mensajes se guardan en PostgreSQL y se pueden revisar en **Panel del taller → Consultas**, donde el administrador puede llamar y marcar el mensaje como respondido. No se envían mensajes automáticos.

El archivo `render.yaml` permite crear una instalación de prueba con Docker y PostgreSQL en Render. Vinculá el repositorio y la rama del PR, y cargá correo y contraseña del administrador mediante el formulario de secretos de Render. La base gratuita vence y las fotos subidas al disco del contenedor se pierden al reiniciar. Usá datos ficticios durante las pruebas.

La portada utiliza una imagen ilustrativa generada de una máquina Godeco en uso. Las fotos del catálogo de demostración provienen de las referencias de Calubridi.

## Interfaz de cuenta y taller

Para desarrollo, desde la raíz ejecutá `npm ci --prefix frontend` y luego `npm run check --prefix frontend` y `npm run build --prefix frontend`. Docker compila estos componentes de React y TypeScript automáticamente.

En pedidos de prueba, la URL autorizada de retorno de Google es `https://calubridi-tienda-pruebas.onrender.com/signin-google`. La persona que administra el taller debe entrar una vez con su contraseña y abrir **Mi cuenta → Vincular Google** antes de usar su Gmail para entrar al panel. Vincular exige confirmar la contraseña y conserva los permisos de la cuenta ya autenticada.
