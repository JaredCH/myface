(() => {
  function initUpload(root) {
    const input = root.querySelector('[data-upload-input]');
    if (!input) {
      return;
    }
    const dropzone = root.querySelector('[data-drop-target]');
    const addButton = root.querySelector('[data-add-trigger]');
    const previewGrid = root.querySelector('[data-preview-grid]');

    const render = () => {
      if (!previewGrid) {
        return;
      }
      const files = Array.from(input.files || []);
      const template = document.createDocumentFragment();
      files.forEach((file, index) => {
        const card = document.createElement('div');
        card.className = 'lux-preview-card';
        const remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'button button-secondary remove';
        remove.textContent = 'Remove';
        remove.addEventListener('click', () => removeFile(index));

        const meta = document.createElement('div');
        meta.className = 'lux-preview-meta';
        meta.innerHTML = `<strong>${escapeHtml(file.name)}</strong><span>${prettyBytes(file.size)}</span>`;

        card.appendChild(meta);
        const img = document.createElement('img');
        img.alt = file.name;
        img.loading = 'lazy';
        const reader = new FileReader();
        reader.onload = e => {
          img.src = e.target?.result;
        };
        reader.readAsDataURL(file);
        card.appendChild(img);
        card.appendChild(remove);
        template.appendChild(card);
      });

      const slotCount = 2 - files.length;
      for (let i = 0; i < slotCount; i += 1) {
        const emptyCard = document.createElement('div');
        emptyCard.className = 'lux-preview-card empty';
        emptyCard.innerHTML = `<span>Slot ${files.length + i + 1}</span>`;
        template.appendChild(emptyCard);
      }

      previewGrid.innerHTML = '';
      previewGrid.appendChild(template);
    };

    const writeFiles = files => {
      const accepted = Array.from(files).slice(0, 2);
      const dt = new DataTransfer();
      accepted.forEach(file => dt.items.add(file));
      input.files = dt.files;
      render();
    };

    const removeFile = index => {
      const files = Array.from(input.files || []);
      files.splice(index, 1);
      const dt = new DataTransfer();
      files.forEach(file => dt.items.add(file));
      input.files = dt.files;
      render();
    };

    const onFilesSelected = event => {
      writeFiles(event.target.files);
    };

    input.addEventListener('change', onFilesSelected);

    if (addButton) {
      addButton.addEventListener('click', () => input.click());
    }

    if (dropzone) {
      ['dragenter', 'dragover'].forEach(evt => {
        dropzone.addEventListener(evt, e => {
          e.preventDefault();
          dropzone.classList.add('dragging');
        });
      });
      ['dragleave', 'drop'].forEach(evt => {
        dropzone.addEventListener(evt, e => {
          e.preventDefault();
          dropzone.classList.remove('dragging');
        });
      });
      dropzone.addEventListener('drop', e => {
        const files = e.dataTransfer?.files;
        if (files?.length) {
          writeFiles(files);
        }
      });
      dropzone.addEventListener('click', () => input.click());
    }

    render();
  }

  function prettyBytes(size) {
    if (size < 1024) {
      return `${size} B`;
    }
    if (size < 1024 * 1024) {
      return `${(size / 1024).toFixed(1)} KB`;
    }
    return `${(size / (1024 * 1024)).toFixed(1)} MB`;
  }

  function escapeHtml(value) {
    const div = document.createElement('div');
    div.innerText = value;
    return div.innerHTML;
  }

  document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-upload-root]').forEach(initUpload);
  });
})();
