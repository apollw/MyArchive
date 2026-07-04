window.myArchiveCover = {
    getDimensions(inputId) {
        const input = document.getElementById(inputId);
        const file = input?.files?.[0];

        if (!file) {
            return Promise.reject(new Error('Nenhum arquivo selecionado.'));
        }

        return new Promise((resolve, reject) => {
            const image = new Image();
            const url = URL.createObjectURL(file);

            image.onload = () => {
                const dimensions = {
                    width: image.naturalWidth,
                    height: image.naturalHeight
                };

                URL.revokeObjectURL(url);
                resolve(dimensions);
            };

            image.onerror = () => {
                URL.revokeObjectURL(url);
                reject(new Error('Nao foi possivel ler as dimensoes da imagem.'));
            };

            image.src = url;
        });
    }
};